using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInventory.Application.Entities;

[Table("suppliers")]
public sealed class Supplier
{
    [Column("id")] public int Id { get; set; }
    [Column("code"), MaxLength(40)] public string Code { get; set; } = string.Empty;
    [Column("name"), MaxLength(200)] public string Name { get; set; } = string.Empty;
    [Column("contact_person"), MaxLength(160)] public string? ContactPerson { get; set; }
    [Column("email"), MaxLength(254)] public string? Email { get; set; }
    [Column("phone"), MaxLength(40)] public string? Phone { get; set; }
    [Column("address"), MaxLength(1000)] public string? Address { get; set; }
    [Column("tax_code"), MaxLength(40)] public string? TaxCode { get; set; }
    [Column("is_active")] public bool IsActive { get; set; } = true;
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
