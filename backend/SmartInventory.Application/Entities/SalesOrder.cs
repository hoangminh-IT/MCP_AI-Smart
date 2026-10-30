using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInventory.Application.Entities;

[Table("sales_orders")]
public sealed class SalesOrder
{
    [Column("warehouse_id")] public int WarehouseId { get; set; } = 1;
    [Column("id")] public int Id { get; set; }
    [Column("code")] public string Code { get; set; } = string.Empty;
    [Column("customer_id")] public int CustomerId { get; set; }
    [Column("status")] public string Status { get; set; } = "Draft";
    [Column("notes")] public string? Notes { get; set; }
    [Column("total_amount", TypeName = "numeric(18,2)")] public decimal TotalAmount { get; set; }
    [Column("created_by")] public int CreatedBy { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("sales_order_items")]
public sealed class SalesOrderItem
{
    [Column("id")] public int Id { get; set; }
    [Column("sales_order_id")] public int SalesOrderId { get; set; }
    [Column("product_id")] public int ProductId { get; set; }
    [Column("quantity")] public int Quantity { get; set; }
    [Column("fulfilled_quantity")] public int FulfilledQuantity { get; set; }
    [Column("unit_price", TypeName = "numeric(18,2)")] public decimal UnitPrice { get; set; }
}

[Table("sales_order_fulfillments")]
public sealed class SalesOrderFulfillment
{
    [Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("sales_order_id")] public int SalesOrderId { get; set; }
    [Column("request_id")] public Guid RequestId { get; set; }
    [Column("payload_fingerprint")] public string PayloadFingerprint { get; set; } = string.Empty;
    [Column("reason")] public string Reason { get; set; } = string.Empty;
    [Column("response_snapshot", TypeName = "jsonb")] public string? ResponseSnapshot { get; set; }
    [Column("created_by")] public int CreatedBy { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("sales_order_fulfillment_items")]
public sealed class SalesOrderFulfillmentItem
{
    [Column("id")] public int Id { get; set; }
    [Column("fulfillment_id")] public Guid FulfillmentId { get; set; }
    [Column("sales_order_item_id")] public int SalesOrderItemId { get; set; }
    [Column("product_id")] public int ProductId { get; set; }
    [Column("quantity")] public int Quantity { get; set; }
    [Column("movement_id")] public int MovementId { get; set; }
}
