using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInventory.Application.Entities;

[Table("purchase_orders")]
public sealed class PurchaseOrder
{
    [Column("warehouse_id")] public int WarehouseId { get; set; } = 1;
    [Column("id")] public int Id { get; set; }
    [Column("code")] public string Code { get; set; } = string.Empty;
    [Column("supplier_id")] public int SupplierId { get; set; }
    [Column("status")] public string Status { get; set; } = "Draft";
    [Column("notes")] public string? Notes { get; set; }
    [Column("total_amount", TypeName = "numeric(18,2)")] public decimal TotalAmount { get; set; }
    [Column("created_by")] public int CreatedBy { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("purchase_order_items")]
public sealed class PurchaseOrderItem
{
    [Column("id")] public int Id { get; set; }
    [Column("purchase_order_id")] public int PurchaseOrderId { get; set; }
    [Column("product_id")] public int ProductId { get; set; }
    [Column("quantity")] public int Quantity { get; set; }
    [Column("unit_cost", TypeName = "numeric(18,2)")] public decimal UnitCost { get; set; }
    [Column("received_quantity")] public int ReceivedQuantity { get; set; }
}

[Table("purchase_order_receipts")]
public sealed class PurchaseOrderReceipt
{
    [Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("purchase_order_id")] public int PurchaseOrderId { get; set; }
    [Column("request_id")] public Guid RequestId { get; set; }
    [Column("payload_fingerprint")] public string PayloadFingerprint { get; set; } = string.Empty;
    [Column("reason")] public string Reason { get; set; } = string.Empty;
    [Column("response_snapshot", TypeName = "jsonb")] public string? ResponseSnapshot { get; set; }
    [Column("created_by")] public int CreatedBy { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("purchase_order_receipt_items")]
public sealed class PurchaseOrderReceiptItem
{
    [Column("id")] public int Id { get; set; }
    [Column("receipt_id")] public Guid ReceiptId { get; set; }
    [Column("purchase_order_item_id")] public int PurchaseOrderItemId { get; set; }
    [Column("product_id")] public int ProductId { get; set; }
    [Column("quantity")] public int Quantity { get; set; }
    [Column("movement_id")] public int MovementId { get; set; }
}
