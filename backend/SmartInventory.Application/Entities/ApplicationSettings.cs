using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInventory.Application.Entities;

[Table("application_settings")]
public sealed class ApplicationSettings
{
    [Column("id")] public int Id { get; set; } = 1;
    [Column("company_name"), MaxLength(200)] public string CompanyName { get; set; } = "AI Smart Inventory";
    [Column("contact_email"), MaxLength(254)] public string? ContactEmail { get; set; }
    [Column("phone"), MaxLength(40)] public string? Phone { get; set; }
    [Column("address"), MaxLength(1000)] public string? Address { get; set; }
    [Column("default_min_stock")] public int DefaultMinStock { get; set; }
    [Column("report_default_days")] public int ReportDefaultDays { get; set; } = 30;
    [Column("revision"), ConcurrencyCheck] public int Revision { get; set; } = 1;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("user_preferences")]
public sealed class UserPreferences
{
    [Column("user_id"), Key] public int UserId { get; set; }
    [Column("default_page_size")] public int DefaultPageSize { get; set; } = 10;
    [Column("revision"), ConcurrencyCheck] public int Revision { get; set; } = 1;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
