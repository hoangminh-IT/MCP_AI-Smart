using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInventory.Application.Entities;

[Table("user_notifications")]
public sealed class UserNotification
{
    [Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("user_id")] public int UserId { get; set; }
    [Column("deduplication_key")] public string DeduplicationKey { get; set; } = string.Empty;
    [Column("type")] public string Type { get; set; } = string.Empty;
    [Column("title")] public string Title { get; set; } = string.Empty;
    [Column("message")] public string Message { get; set; } = string.Empty;
    [Column("target_type")] public string? TargetType { get; set; }
    [Column("target_id")] public string? TargetId { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("read_at")] public DateTime? ReadAt { get; set; }
}
