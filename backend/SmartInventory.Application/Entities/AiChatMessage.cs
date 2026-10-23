using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInventory.Application.Entities;

[Table("ai_chat_messages")]
public sealed class AiChatMessage
{
    [Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("session_id")] public Guid SessionId { get; set; }
    [Column("role")] public string Role { get; set; } = string.Empty;
    [Column("content")] public string Content { get; set; } = string.Empty;
    [Column("tool_metadata")] public string? ToolMetadata { get; set; }
    [Column("status")] public string Status { get; set; } = "Succeeded";
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
