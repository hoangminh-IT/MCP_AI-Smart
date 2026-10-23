using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInventory.Application.Entities;

[Table("products")]
public class Product
{
    [Column("id")]
    public int Id { get; set; }

    [Column("category_id")]
    public int CategoryId { get; set; }

    [Column("sku")]
    public string Sku { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("price", TypeName = "numeric(18,2)")]
    public decimal Price { get; set; }

    [Column("cost_price", TypeName = "numeric(18,2)")]
    public decimal CostPrice { get; set; }

    [Column("min_stock")]
    public int MinStock { get; set; } = 10;

    [Column("image_url")]
    public string? ImageUrl { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
