using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Audit;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Audit;

public sealed class AuditService(AppDbContext database) : IAuditService
{
    public async Task<PagedResult<AuditEntryDto>> GetAuditAsync(int actorId, AuditQuery query, CancellationToken cancellationToken)
    {
        if (!await database.Users.AsNoTracking().AnyAsync(user => user.Id == actorId && user.IsActive && user.Role == UserRoles.Admin, cancellationToken))
            throw new AppForbiddenException("An active administrator account is required.");
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue)
            throw new AppValidationException("page must be positive and pageSize must be between 1 and 100.");
        if (query.ActorId is <= 0 || query.Action?.Length > 80 || query.EntityName?.Length > 80 || query.Status?.Length > 32)
            throw new AppValidationException("actorId must be positive; action/entityName allow 80 characters and status 32.");
        var source = string.IsNullOrWhiteSpace(query.Source) ? null : query.Source.Trim().ToLowerInvariant();
        if (source is not (null or "audit" or "mcp" or "ai")) throw new AppValidationException("source must be audit, mcp or ai.");
        var from = query.From?.UtcDateTime;
        var to = query.To?.UtcDateTime;
        if (from > to) throw new AppValidationException("from must be before or equal to to.");

        var audit = from entry in database.AuditLogs.AsNoTracking()
            join user in database.Users.AsNoTracking() on entry.ActorId equals (int?)user.Id into actors
            from actor in actors.DefaultIfEmpty()
            select new AuditRow { Id = "audit:" + entry.Id, Source = "audit", ActorId = entry.ActorId,
                ActorUsername = actor == null ? null : actor.Username, Action = entry.Action, EntityName = entry.EntityName,
                TargetId = entry.TargetId, Status = entry.Status, CreatedAt = entry.CreatedAt };
        var mcp = from entry in database.McpToolExecutionLogs.AsNoTracking()
            join user in database.Users.AsNoTracking() on entry.UserId equals user.Id
            select new AuditRow { Id = "mcp:" + entry.Id, Source = "mcp", ActorId = entry.UserId,
                ActorUsername = user.Username, Action = "ToolExecuted", EntityName = "McpTool", TargetId = entry.ToolName,
                Status = entry.Status, CreatedAt = entry.CreatedAt };
        var ai = from entry in database.AiChatMessages.AsNoTracking()
            join session in database.AiChatSessions.AsNoTracking() on entry.SessionId equals session.Id
            join user in database.Users.AsNoTracking() on session.UserId equals user.Id
            where entry.Role == "assistant"
            select new AuditRow { Id = "ai:" + entry.Id, Source = "ai", ActorId = user.Id,
                ActorUsername = user.Username, Action = "MessageCompleted", EntityName = "AiChatSession", TargetId = session.Id.ToString(),
                Status = entry.Status, CreatedAt = entry.CreatedAt };
        var entries = source switch { "audit" => audit, "mcp" => mcp, "ai" => ai, _ => audit.Concat(mcp).Concat(ai) };
        if (query.ActorId.HasValue) entries = entries.Where(entry => entry.ActorId == query.ActorId.Value);
        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim().ToLowerInvariant();
            entries = entries.Where(entry => entry.Action.ToLower() == action);
        }
        if (!string.IsNullOrWhiteSpace(query.EntityName))
        {
            var entity = query.EntityName.Trim().ToLowerInvariant();
            entries = entries.Where(entry => entry.EntityName.ToLower() == entity);
        }
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim().ToLowerInvariant();
            entries = entries.Where(entry => entry.Status.ToLower() == status);
        }
        if (from.HasValue) entries = entries.Where(entry => entry.CreatedAt >= from.Value);
        if (to.HasValue) entries = entries.Where(entry => entry.CreatedAt <= to.Value);
        var count = await entries.CountAsync(cancellationToken);
        var items = await entries.OrderByDescending(entry => entry.CreatedAt).ThenByDescending(entry => entry.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(entry => new AuditEntryDto(entry.Id, entry.Source, entry.ActorId, entry.ActorUsername,
                entry.Action, entry.EntityName, entry.TargetId, entry.Status, entry.CreatedAt)).ToListAsync(cancellationToken);
        return new(items, count, query.Page, query.PageSize);
    }

    private sealed class AuditRow
    {
        public string Id { get; init; } = string.Empty;
        public string Source { get; init; } = string.Empty;
        public int? ActorId { get; init; }
        public string? ActorUsername { get; init; }
        public string Action { get; init; } = string.Empty;
        public string EntityName { get; init; } = string.Empty;
        public string? TargetId { get; init; }
        public string Status { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
    }
}
