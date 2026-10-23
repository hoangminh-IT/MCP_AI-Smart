using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInventory.Application.Entities;

[Table("users")]
public class User
{
    [Column("id")]
    public int Id { get; set; }

    [Column("username")]
    public string Username { get; set; } = string.Empty;

    [Column("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("role")]
    public string Role { get; set; } = "Staff";

    [Column("email")]
    [MaxLength(254)]
    public string Email { get; set; } = string.Empty;

    [Column("full_name")]
    [MaxLength(160)]
    public string FullName { get; set; } = string.Empty;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("token_version")]
    [ConcurrencyCheck]
    public int TokenVersion { get; set; }
}
