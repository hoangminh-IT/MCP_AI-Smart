using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInventory.Application.Entities;

[Table("password_reset_tokens")]
public sealed class PasswordResetToken
{
    [Column("id")] public Guid Id { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    [Column("token_hash")] public string TokenHash { get; set; } = string.Empty;
    [Column("token_version")] public int TokenVersion { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("expires_at")] public DateTime ExpiresAt { get; set; }
    [Column("consumed_at")] public DateTime? ConsumedAt { get; set; }
}
