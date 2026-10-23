using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInventory.Application.Entities;

[Table("mcp_tool_execution_logs")]
public sealed class McpToolExecutionLog
{
    [Column("id")] public int Id { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    [Column("tool_name")] public string ToolName { get; set; } = string.Empty;
    [Column("sanitized_arguments", TypeName = "jsonb")] public string SanitizedArguments { get; set; } = "{}";
    [Column("sanitized_output", TypeName = "jsonb")] public string? SanitizedOutput { get; set; }
    [Column("status")] public string Status { get; set; } = string.Empty;
    [Column("duration_ms")] public long DurationMs { get; set; }
    [Column("error_message")] public string? ErrorMessage { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
