using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInventory.Application.Entities;

/// <summary>Only a SHA-256 credential hash is persisted; raw refresh tokens leave the API in HttpOnly cookies.</summary>
[Table("refresh_sessions")]
public sealed class RefreshSession
{
    [Column("id")] public Guid Id { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    [Column("family_id")] public Guid FamilyId { get; set; }
    [Column("token_hash"), MaxLength(64)] public string TokenHash { get; set; } = "";
    [Column("token_version")] public int TokenVersion { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("expires_at")] public DateTime ExpiresAt { get; set; }
    [Column("consumed_at")] public DateTime? ConsumedAt { get; set; }
    [Column("replaced_by_id")] public Guid? ReplacedById { get; set; }
    [Column("revoked_at")] public DateTime? RevokedAt { get; set; }
}
