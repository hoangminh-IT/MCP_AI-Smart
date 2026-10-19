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

    [Column("price", TypeName = "numeric(18,2)")]
    public decimal Price { get; set; }

    [Column("min_stock")]
    public int MinStock { get; set; } = 10;
}
