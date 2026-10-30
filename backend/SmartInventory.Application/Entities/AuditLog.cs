using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInventory.Application.Entities;

/// <summary>Append-only operation metadata. No submitted values, credentials or request bodies are stored.</summary>
[Table("audit_logs")]
public sealed class AuditLog
{
    [Column("id")] public long Id { get; set; }
    [Column("actor_id")] public int? ActorId { get; set; }
    [Column("action"), MaxLength(80)] public string Action { get; set; } = string.Empty;
    [Column("entity_name"), MaxLength(80)] public string EntityName { get; set; } = string.Empty;
    [Column("target_id"), MaxLength(100)] public string? TargetId { get; set; }
    [Column("status"), MaxLength(32)] public string Status { get; set; } = "Succeeded";
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
