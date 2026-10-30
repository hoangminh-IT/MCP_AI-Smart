using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInventory.Application.Entities;

[Table("stock_movements")]
public class StockMovement
{
    [Column("warehouse_id")] public int WarehouseId { get; set; } = 1;
    [Column("warehouse_quantity_before")] public int? WarehouseQuantityBefore { get; set; }
    [Column("warehouse_quantity_after")] public int? WarehouseQuantityAfter { get; set; }
    [Column("id")]
    public int Id { get; set; }

    [Column("product_id")]
    public int ProductId { get; set; }

    [Column("type")]
    public string Type { get; set; } = string.Empty;

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("quantity_before")]
    public int? QuantityBefore { get; set; }

    [Column("quantity_after")]
    public int? QuantityAfter { get; set; }

    [Column("reason")]
    public string Reason { get; set; } = string.Empty;

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("request_id")]
    public Guid? RequestId { get; set; }

    [Column("reference_type")]
    public string? ReferenceType { get; set; }

    [Column("reference_id")]
    public string? ReferenceId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } =
        DateTime.UtcNow;
}
