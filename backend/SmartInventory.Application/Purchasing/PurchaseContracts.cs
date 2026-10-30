using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Stock;

namespace SmartInventory.Application.Purchasing;

public static class PurchaseStatuses
{
    public const string Draft = "Draft", Submitted = "Submitted", Approved = "Approved",
        PartiallyReceived = "PartiallyReceived", Received = "Received", Cancelled = "Cancelled";
}

public sealed class PurchaseOrderQuery
{
    public int? WarehouseId { get; set; }
    public string? Search { get; set; }
    public int? SupplierId { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string SortBy { get; set; } = "createdAt";
    public bool Descending { get; set; } = true;
}

public sealed class PurchaseOrderRequest
{
    [Range(1, int.MaxValue)] public int? WarehouseId { get; set; }
    [Range(1, int.MaxValue)] public int SupplierId { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
    [Required, MinLength(1), MaxLength(100)] public List<PurchaseItemRequest> Items { get; set; } = [];
}

public sealed class PurchaseItemRequest
{
    [Range(1, int.MaxValue)] public int ProductId { get; set; }
    [Range(1, int.MaxValue)] public int Quantity { get; set; }
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    [Range(typeof(decimal), "0", "9999999999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal UnitCost { get; set; }
}

public sealed class PurchaseReceiptRequest
{
    public Guid RequestId { get; set; }
    [StringLength(1000)] public string? Reason { get; set; }
    [Required, MinLength(1), MaxLength(100)] public List<PurchaseReceiptLineRequest> Items { get; set; } = [];
}

public sealed class PurchaseReceiptLineRequest
{
    [Range(1, int.MaxValue)] public int PurchaseOrderItemId { get; set; }
    [Range(1, int.MaxValue)] public int Quantity { get; set; }
}

public sealed record PurchaseOrderSummaryDto(int Id, string Code, int SupplierId, string SupplierName,
    string Status, decimal TotalAmount, DateTime CreatedAt, DateTime UpdatedAt, long OrderedQuantity, long ReceivedQuantity, int WarehouseId = 1, string? WarehouseCode = null, string? WarehouseName = null);
public sealed record PurchaseItemDto(int Id, int ProductId, string Sku, string ProductName,
    int Quantity, decimal UnitCost, decimal LineTotal, int ReceivedQuantity);
public sealed record PurchaseReceiptLineDto(int PurchaseOrderItemId, int ProductId, int Quantity, int MovementId);
public sealed record PurchaseReceiptDto(Guid Id, Guid RequestId, DateTime CreatedAt, int CreatedBy, IReadOnlyList<PurchaseReceiptLineDto> Items);
public sealed record PurchaseOrderDetailDto(int Id, string Code, int SupplierId, string SupplierName,
    string Status, decimal TotalAmount, DateTime CreatedAt, DateTime UpdatedAt, long OrderedQuantity,
    long ReceivedQuantity, string? Notes, int CreatedBy, IReadOnlyList<PurchaseItemDto> Items, IReadOnlyList<PurchaseReceiptDto> Receipts, int WarehouseId = 1, string? WarehouseCode = null, string? WarehouseName = null);
public sealed record PurchaseReceiptResult(Guid ReceiptId, Guid RequestId, bool Replayed,
    PurchaseOrderDetailDto Order, IReadOnlyList<StockOperationDto> Movements);

public interface IPurchaseOrderService
{
    Task<PagedResult<PurchaseOrderSummaryDto>> ListAsync(PurchaseOrderQuery query, CancellationToken cancellationToken);
    Task<PurchaseOrderDetailDto> GetAsync(int id, CancellationToken cancellationToken);
    Task<PurchaseOrderDetailDto> CreateAsync(int userId, PurchaseOrderRequest request, CancellationToken cancellationToken);
    Task<PurchaseOrderDetailDto> UpdateAsync(int userId, int id, PurchaseOrderRequest request, CancellationToken cancellationToken);
    Task<PurchaseOrderDetailDto> SubmitAsync(int userId, int id, CancellationToken cancellationToken);
    Task<PurchaseOrderDetailDto> ApproveAsync(int userId, int id, CancellationToken cancellationToken);
    Task<PurchaseOrderDetailDto> CancelAsync(int userId, int id, CancellationToken cancellationToken);
    Task<PurchaseReceiptResult> ReceiveAsync(int userId, int id, PurchaseReceiptRequest request, CancellationToken cancellationToken);
}
