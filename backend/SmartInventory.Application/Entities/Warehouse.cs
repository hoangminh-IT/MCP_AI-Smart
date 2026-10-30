using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInventory.Application.Entities;

[Table("warehouses")]
public sealed class Warehouse
{
    [Column("id")] public int Id { get; set; }
    [Column("code")] public string Code { get; set; } = "";
    [Column("name")] public string Name { get; set; } = "";
    [Column("address")] public string? Address { get; set; }
    [Column("is_active")] public bool IsActive { get; set; } = true;
    [Column("is_default")] public bool IsDefault { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("warehouse_inventory")]
public sealed class WarehouseInventory
{
    [Column("product_id")] public int ProductId { get; set; }
    [Column("warehouse_id")] public int WarehouseId { get; set; }
    [Column("quantity")] public int Quantity { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("stock_transfers")]
public sealed class StockTransfer
{
    [Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("request_id")] public Guid RequestId { get; set; }
    [Column("product_id")] public int ProductId { get; set; }
    [Column("source_warehouse_id")] public int SourceWarehouseId { get; set; }
    [Column("destination_warehouse_id")] public int DestinationWarehouseId { get; set; }
    [Column("quantity")] public int Quantity { get; set; }
    [Column("reason")] public string Reason { get; set; } = "";
    [Column("payload_fingerprint")] public string PayloadFingerprint { get; set; } = "";
    [Column("response_snapshot", TypeName = "jsonb")] public string? ResponseSnapshot { get; set; }
    [Column("created_by")] public int CreatedBy { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
