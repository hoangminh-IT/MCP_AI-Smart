using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInventory.Application.Entities;

[Table("stock_movements")]
public class StockMovement
{
    [Column("id")]
    public int Id { get; set; }

    [Column("product_id")]
    public int ProductId { get; set; }

    [Column("type")]
    public string Type { get; set; } = string.Empty;

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("reason")]
    public string Reason { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } =
        DateTime.UtcNow;
}
