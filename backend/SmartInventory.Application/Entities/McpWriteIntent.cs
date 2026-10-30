using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInventory.Application.Entities;

[Table("mcp_write_intents")]
public sealed class McpWriteIntent
{
    [Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("user_id")] public int UserId { get; set; }
    [Column("tool_name"), MaxLength(128)] public string ToolName { get; set; } = string.Empty;
    [Column("arguments", TypeName = "jsonb")] public string Arguments { get; set; } = "{}";
    [Column("arguments_hash"), MaxLength(64)] public string ArgumentsHash { get; set; } = string.Empty;
    [Column("status"), MaxLength(32)] public string Status { get; set; } = "Pending";
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("expires_at")] public DateTime ExpiresAt { get; set; }
    [Column("session_id")] public Guid? SessionId { get; set; }
    [Column("proof_hash"), MaxLength(64)] public string? ProofHash { get; set; }
    [Column("result", TypeName = "jsonb")] public string? Result { get; set; }
    [Column("error_message"), MaxLength(1000)] public string? ErrorMessage { get; set; }
}
