using System.ComponentModel.DataAnnotations;
using SmartInventory.Application.Catalog;

namespace SmartInventory.Application.Notifications;

public sealed class NotificationQuery
{
    public bool? IsRead { get; set; }
    public string? Type { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed record NotificationDto(Guid Id, string Type, string Title, string Message,
    string? TargetType, string? TargetId, DateTime CreatedAt, DateTime? ReadAt)
{
    public bool IsRead => ReadAt.HasValue;
}

public sealed record NotificationSummaryDto(int UnreadCount);

public interface INotificationService
{
    Task PublishAsync(string deduplicationKey, string type, string title, string message,
        string? targetType, string? targetId, CancellationToken cancellationToken);
    Task<PagedResult<NotificationDto>> ListAsync(int userId, NotificationQuery query, CancellationToken cancellationToken);
    Task<NotificationSummaryDto> GetSummaryAsync(int userId, CancellationToken cancellationToken);
    Task MarkReadAsync(int userId, Guid id, CancellationToken cancellationToken);
    Task MarkAllReadAsync(int userId, CancellationToken cancellationToken);
}
