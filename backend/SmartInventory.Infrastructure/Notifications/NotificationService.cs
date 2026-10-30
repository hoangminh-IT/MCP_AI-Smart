using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;
using SmartInventory.Application.Notifications;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Notifications;

public sealed class NotificationService(AppDbContext database) : INotificationService
{
    public async Task PublishAsync(string deduplicationKey, string type, string title, string message,
        string? targetType, string? targetId, CancellationToken cancellationToken)
    {
        if (deduplicationKey.Length is < 1 or > 160 || title.Length is < 1 or > 160 || message.Length is < 1 or > 1000 ||
            targetType?.Length > 40 || targetId?.Length > 100 ||
            (targetType is null) != (targetId is null) ||
            type is not ("LowStock" or "OutOfStock" or "PurchaseOrder" or "SalesOrder"))
            throw new AppValidationException("Notification values exceed the supported limits or use an unknown type.");

        var createdAt = DateTime.UtcNow;
        // One statement makes concurrent publication idempotent without aborting the caller's transaction.
        // Recipient ordering also keeps concurrent publishers' unique-key lock order consistent.
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO user_notifications
                (id, user_id, deduplication_key, type, title, message, target_type, target_id, created_at, read_at)
            SELECT gen_random_uuid(), recipient.id, {deduplicationKey}, {type}, {title}, {message},
                {targetType}, {targetId}, {createdAt}, NULL
            FROM users AS recipient
            WHERE recipient.is_active AND recipient.role IN ({UserRoles.Admin}, {UserRoles.Staff})
            ORDER BY recipient.id
            ON CONFLICT (user_id, deduplication_key) DO NOTHING
            """, cancellationToken);
    }

    public async Task<PagedResult<NotificationDto>> ListAsync(int userId, NotificationQuery query, CancellationToken cancellationToken)
    {
        ValidatePage(query.Page, query.PageSize);
        var notifications = database.UserNotifications.AsNoTracking().Where(item => item.UserId == userId);
        if (query.IsRead.HasValue) notifications = notifications.Where(item => (item.ReadAt != null) == query.IsRead.Value);
        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            var type = query.Type.Trim();
            if (type.Length > 40 || type is not ("LowStock" or "OutOfStock" or "PurchaseOrder" or "SalesOrder"))
                throw new AppValidationException("type must be LowStock, OutOfStock, PurchaseOrder or SalesOrder.");
            notifications = notifications.Where(item => item.Type == type);
        }
        var count = await notifications.CountAsync(cancellationToken);
        var items = await notifications.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(item => new NotificationDto(item.Id, item.Type, item.Title, item.Message,
                item.TargetType, item.TargetId, item.CreatedAt, item.ReadAt)).ToListAsync(cancellationToken);
        return new(items, count, query.Page, query.PageSize);
    }

    public async Task<NotificationSummaryDto> GetSummaryAsync(int userId, CancellationToken cancellationToken) =>
        new(await database.UserNotifications.CountAsync(item => item.UserId == userId && item.ReadAt == null, cancellationToken));

    public async Task MarkReadAsync(int userId, Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) throw new AppValidationException("A valid notification ID is required.");
        var updated = await database.UserNotifications.Where(item => item.UserId == userId && item.Id == id && item.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ReadAt, DateTime.UtcNow), cancellationToken);
        if (updated == 0 && !await database.UserNotifications.AnyAsync(item => item.UserId == userId && item.Id == id, cancellationToken))
            throw new AppNotFoundException("Notification was not found.");
    }

    public async Task MarkAllReadAsync(int userId, CancellationToken cancellationToken) =>
        _ = await database.UserNotifications.Where(item => item.UserId == userId && item.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ReadAt, DateTime.UtcNow), cancellationToken);

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new AppValidationException("page must be positive and pageSize must be between 1 and 100.");
    }
}
