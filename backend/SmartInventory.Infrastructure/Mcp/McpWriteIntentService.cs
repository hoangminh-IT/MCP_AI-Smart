using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Common;
using SmartInventory.Application.Entities;
using SmartInventory.Application.Mcp;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Mcp;

/// <summary>Persists user-reviewed write intents and atomically consumes their private confirmations with business effects.</summary>
public sealed class McpWriteIntentService(AppDbContext database) : IMcpWriteIntentService, IMcpWriteGuard
{
    public async Task<McpWriteIntentDto> CreateAsync(int userId, string toolName, JsonElement arguments, Guid? sessionId,
        CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var canonical = McpWritePayload.Normalize(toolName, arguments, id);
        await RequirePermissionAsync(userId, toolName, canonical, cancellationToken);
        await RequireSessionAsync(userId, sessionId, cancellationToken);
        var now = DateTime.UtcNow;
        var intent = new McpWriteIntent { Id = id, UserId = userId, ToolName = toolName,
            Arguments = canonical.GetRawText(), ArgumentsHash = Hash(canonical.GetRawText()),
            Status = McpWriteStatuses.Pending, SessionId = sessionId, CreatedAt = now, ExpiresAt = now.AddMinutes(5) };
        database.McpWriteIntents.Add(intent);
        await database.SaveChangesAsync(cancellationToken);
        return ToDto(intent);
    }

    public async Task<McpWriteIntentDto> GetAsync(int userId, Guid id, CancellationToken cancellationToken)
    {
        await RequireUserAsync(userId, cancellationToken);
        return ToDto(await database.McpWriteIntents.AsNoTracking().SingleOrDefaultAsync(intent => intent.Id == id && intent.UserId == userId, cancellationToken)
            ?? throw new AppNotFoundException("Write intent was not found."));
    }

    public async Task<IReadOnlyList<McpWriteIntentDto>> ListAsync(int userId, Guid? sessionId, CancellationToken cancellationToken)
    {
        await RequireUserAsync(userId, cancellationToken);
        await RequireSessionAsync(userId, sessionId, cancellationToken);
        var query = database.McpWriteIntents.AsNoTracking().Where(intent => intent.UserId == userId);
        if (sessionId.HasValue) query = query.Where(intent => intent.SessionId == sessionId.Value);
        return (await query.OrderByDescending(intent => intent.CreatedAt).ThenByDescending(intent => intent.Id)
            .Take(50).ToListAsync(cancellationToken)).Select(ToDto).ToArray();
    }

    public async Task<McpWriteApproval> ApproveAsync(int userId, Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var intent = await LockOwnedAsync(userId, id, cancellationToken);
        var arguments = Json(intent.Arguments);
        await RequirePermissionAsync(userId, intent.ToolName, arguments, cancellationToken);
        if (intent.Status == McpWriteStatuses.Succeeded)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(ToDto(intent), string.Empty);
        }
        RequireLive(intent);
        var proof = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        // Re-approval rotates an unconsumed proof. The row lock serializes it against an executing guard.
        intent.ProofHash = Hash(proof);
        intent.Status = McpWriteStatuses.Approved;
        intent.ErrorMessage = null;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(ToDto(intent), proof);
    }

    public async Task<McpWriteIntentDto> CancelAsync(int userId, Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await RequireUserAsync(userId, cancellationToken);
        var intent = await LockOwnedAsync(userId, id, cancellationToken);
        if (intent.Status == McpWriteStatuses.Succeeded)
            throw new AppConflictException("A completed write cannot be cancelled or reversed through its intent.");
        intent.Status = McpWriteStatuses.Cancelled;
        intent.ProofHash = null;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(intent);
    }

    public async Task<JsonElement> ExecuteAsync(int userId, string toolName, JsonElement arguments, string confirmationToken,
        Func<CancellationToken, Task<JsonElement>> operation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(confirmationToken) || confirmationToken.Length != 64 || !confirmationToken.All(char.IsAsciiHexDigit))
            throw new AppForbiddenException("This write requires an explicit authenticated confirmation.");
        var canonical = McpWritePayload.Normalize(toolName, arguments);
        var proofHash = Hash(confirmationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var intent = await database.McpWriteIntents.FromSqlInterpolated(
                    $"SELECT * FROM mcp_write_intents WHERE proof_hash = {proofHash} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new AppForbiddenException("The confirmation is invalid, unavailable or superseded.");
            // A reused request scope must not authorize using a stale tracked ticket.
            await database.Entry(intent).ReloadAsync(cancellationToken);
            if (intent.UserId != userId || intent.ToolName != toolName || intent.ArgumentsHash != Hash(canonical.GetRawText()) ||
                intent.ProofHash is null || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(intent.ProofHash), Convert.FromHexString(proofHash)))
                throw new AppForbiddenException("The confirmation does not authorize this user, tool and argument payload.");
            await RequirePermissionAsync(userId, toolName, canonical, cancellationToken);
            if (intent.Status == McpWriteStatuses.Succeeded)
            {
                var replay = intent.Result is null ? throw new AppConflictException("The completed intent has no stored result.") : Json(intent.Result);
                await transaction.CommitAsync(cancellationToken);
                return replay;
            }
            RequireLive(intent);
            if (intent.Status != McpWriteStatuses.Approved) throw new AppForbiddenException("The write has not been approved.");
            var result = await operation(cancellationToken);
            if (result.ValueKind != JsonValueKind.Object || result.TryGetProperty("isError", out var error) && error.ValueKind == JsonValueKind.True)
                throw new AppConflictException("The confirmed tool failed. No business changes were committed.");
            if (Encoding.UTF8.GetByteCount(result.GetRawText()) > 1024 * 1024)
                throw new AppValidationException("The confirmed result exceeds its supported storage limit.");
            intent.Status = McpWriteStatuses.Succeeded;
            intent.Result = result.GetRawText();
            intent.ErrorMessage = null;
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result.Clone();
        }
        catch
        {
            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch
            {
                // A failed deferred COMMIT may already have completed the provider transaction.
                // Never replace its original error with a secondary rollback/connection error.
            }
            finally
            {
                // Never let execution-log persistence re-save pending business
                // entities, even when connection uncertainty prevents rollback.
                database.ChangeTracker.Clear();
            }
            throw;
        }
    }

    private async Task<McpWriteIntent> LockOwnedAsync(int userId, Guid id, CancellationToken cancellationToken)
    {
        var intent = await database.McpWriteIntents.FromSqlInterpolated(
            $"SELECT * FROM mcp_write_intents WHERE id = {id} AND user_id = {userId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken)
            ?? throw new AppNotFoundException("Write intent was not found.");
        await database.Entry(intent).ReloadAsync(cancellationToken);
        return intent;
    }

    private async Task RequirePermissionAsync(int userId, string toolName, JsonElement arguments, CancellationToken cancellationToken) =>
        McpWritePayload.RequirePermission(await RequireUserAsync(userId, cancellationToken), toolName, arguments);

    private async Task<string> RequireUserAsync(int userId, CancellationToken cancellationToken)
    {
        var role = await database.Users.AsNoTracking().Where(user => user.Id == userId && user.IsActive)
            .Select(user => user.Role).SingleOrDefaultAsync(cancellationToken);
        return role is not null && UserRoles.CanManageInventory(role) ? role : throw new AppForbiddenException("An active inventory operator account is required.");
    }

    private async Task RequireSessionAsync(int userId, Guid? sessionId, CancellationToken cancellationToken)
    {
        if (sessionId.HasValue && !await database.AiChatSessions.AsNoTracking()
            .AnyAsync(session => session.Id == sessionId.Value && session.UserId == userId, cancellationToken))
            throw new AppNotFoundException("Chat session was not found.");
    }

    private static void RequireLive(McpWriteIntent intent)
    {
        if (intent.Status == McpWriteStatuses.Cancelled) throw new AppConflictException("The write intent was cancelled.");
        if (intent.ExpiresAt <= DateTime.UtcNow) throw new AppConflictException("The write intent expired. Review a new intent before proceeding.");
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static JsonElement Json(string value) { using var document = JsonDocument.Parse(value); return document.RootElement.Clone(); }
    private static McpWriteIntentDto ToDto(McpWriteIntent intent) => new(intent.Id, intent.ToolName, Json(intent.Arguments),
        intent.Status is McpWriteStatuses.Pending or McpWriteStatuses.Approved && intent.ExpiresAt <= DateTime.UtcNow
            ? McpWriteStatuses.Expired : intent.Status, intent.CreatedAt, intent.ExpiresAt, intent.SessionId,
        intent.Result is null ? null : Json(intent.Result), intent.ErrorMessage);
}
