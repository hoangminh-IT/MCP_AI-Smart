using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Common;
using SmartInventory.Application.Entities;

namespace SmartInventory.Infrastructure.Authentication;

public sealed partial class AuthService
{
    private const int RefreshLifetimeDays = 14;

    public async Task<AuthSession> RefreshAsync(string rawToken, int? expectedUserId,
        CancellationToken cancellationToken = default)
    {
        var credential = await FindCredentialAsync(rawToken, cancellationToken)
            ?? throw InvalidRefresh();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // All login/refresh/logout paths lock user first, then credential. Password/role updates
        // take the same PostgreSQL user row lock through UPDATE, so a stale version cannot rotate.
        var user = await LockUserAsync(credential.UserId, cancellationToken);
        var current = await db.RefreshSessions.FromSqlInterpolated(
            $"SELECT * FROM refresh_sessions WHERE id = {credential.Id} FOR UPDATE").SingleAsync(cancellationToken);
        await db.Entry(current).ReloadAsync(cancellationToken);
        RequireExpectedUser(user.Id, expectedUserId);
        var now = DateTime.UtcNow;
        if (current.RevokedAt.HasValue || !user.IsActive || !UserRoles.IsValid(user.Role) ||
            current.TokenVersion != user.TokenVersion || current.ExpiresAt <= now) throw InvalidRefresh();
        if (current.ConsumedAt.HasValue)
        {
            // Possession of an already consumed credential is replay. Revoke access and every
            // refresh session for this user. Commit revocation before returning Unauthorized.
            user.TokenVersion = checked(user.TokenVersion + 1);
            user.UpdatedAt = now;
            await RevokeSessionsAsync(user.Id, now, cancellationToken);
            AddSessionAudit(user.Id, "RefreshReplayDetected", now);
            await SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw InvalidRefresh();
        }
        var next = CreateSession(user, now, current.FamilyId, current.ExpiresAt);
        current.ConsumedAt = now;
        current.ReplacedById = next.Id;
        db.RefreshSessions.Add(next.Entity);
        AddSessionAudit(user.Id, "SessionRefreshed", now);
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SessionResponse(user, next);
    }

    public async Task<bool> LogoutSessionAsync(string rawToken, int? expectedUserId,
        CancellationToken cancellationToken = default)
    {
        var credential = await FindCredentialAsync(rawToken, cancellationToken);
        if (credential is null) return false;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockUserAsync(credential.UserId, cancellationToken);
        RequireExpectedUser(user.Id, expectedUserId);
        await db.Entry(credential).ReloadAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var revoked = false;
        if (!credential.RevokedAt.HasValue && credential.ExpiresAt > now &&
            credential.TokenVersion == user.TokenVersion)
        {
            user.TokenVersion = checked(user.TokenVersion + 1);
            user.UpdatedAt = now;
            await RevokeSessionsAsync(user.Id, now, cancellationToken);
            AddSessionAudit(user.Id, "LoggedOut", now);
            await SaveChangesAsync(cancellationToken);
            revoked = true;
        }
        await transaction.CommitAsync(cancellationToken);
        return revoked;
    }

    public async Task<int?> GetRefreshSessionOwnerAsync(string rawToken, CancellationToken cancellationToken = default) =>
        (await FindCredentialAsync(rawToken, cancellationToken))?.UserId;

    private async Task<User> LockUserAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {userId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) ?? throw InvalidRefresh();
        await db.Entry(user).ReloadAsync(cancellationToken);
        return user;
    }

    private async Task<RefreshSession?> FindCredentialAsync(string rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(rawToken) || rawToken.Length != 161 || rawToken[32] != '.' ||
            !Guid.TryParseExact(rawToken.AsSpan(0, 32), "N", out var id) ||
            !rawToken.AsSpan(33).ToString().All(char.IsAsciiHexDigit)) return null;
        var session = await db.RefreshSessions.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (session is null || !CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(session.TokenHash), SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)))) return null;
        return session;
    }

    private RefreshCredential CreateSession(User user, DateTime now, Guid? familyId = null, DateTime? expiresAt = null)
    {
        var id = Guid.NewGuid();
        var raw = id.ToString("N") + "." + Convert.ToHexString(RandomNumberGenerator.GetBytes(64));
        var entity = new RefreshSession { Id = id, UserId = user.Id, FamilyId = familyId ?? Guid.NewGuid(),
            TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))),
            TokenVersion = user.TokenVersion, CreatedAt = now, ExpiresAt = expiresAt ?? now.AddDays(RefreshLifetimeDays) };
        return new(entity, raw);
    }

    private AuthSession SessionResponse(User user, RefreshCredential credential)
    {
        var token = tokenIssuer.Issue(user);
        return new(new LoginResponse(token.Value, token.ExpiresAt, UserProfile.FromUser(user)),
            credential.RawToken, credential.Entity.ExpiresAt);
    }

    private Task<int> RevokeSessionsAsync(int userId, DateTime now, CancellationToken cancellationToken) =>
        db.RefreshSessions.Where(item => item.UserId == userId && item.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RevokedAt, now), cancellationToken);

    private void AddSessionAudit(int userId, string action, DateTime now) =>
        db.AuditLogs.Add(new AuditLog { ActorId = userId, Action = action, EntityName = nameof(User),
            TargetId = userId.ToString(CultureInfo.InvariantCulture), Status = "Succeeded", CreatedAt = now });

    private static void RequireExpectedUser(int userId, int? expectedUserId)
    {
        if (expectedUserId.HasValue && expectedUserId.Value != userId)
            throw new AppConflictException("The browser session belongs to another account. Sign in again.");
    }

    private static AppUnauthorizedException InvalidRefresh() =>
        new("The session cannot be renewed. Sign in again.");

    private sealed record RefreshCredential(RefreshSession Entity, string RawToken)
    {
        public Guid Id => Entity.Id;
    }
}
