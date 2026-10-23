using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInventory.Application.Entities;

[Table("ai_chat_sessions")]
public sealed class AiChatSession
{
    [Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("user_id")] public int UserId { get; set; }
    [Column("title")] public string Title { get; set; } = "New conversation";
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    [Column("revision")] public int Revision { get; set; }
    [Column("processing_token")] public Guid? ProcessingToken { get; set; }
    [Column("processing_until")] public DateTime? ProcessingUntil { get; set; }
}
