using SmartInventory.Application.Catalog;

namespace SmartInventory.Application.Audit;

public sealed class AuditQuery
{
    public string? Source { get; set; }
    public int? ActorId { get; set; }
    public string? Action { get; set; }
    public string? EntityName { get; set; }
    public string? Status { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public sealed record AuditEntryDto(string Id, string Source, int? ActorId, string? ActorUsername,
    string Action, string EntityName, string? TargetId, string Status, DateTime CreatedAt);

public interface IAuditService
{
    Task<PagedResult<AuditEntryDto>> GetAuditAsync(int actorId, AuditQuery query, CancellationToken cancellationToken);
}
