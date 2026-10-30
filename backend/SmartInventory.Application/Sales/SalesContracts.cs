using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Stock;

namespace SmartInventory.Application.Sales;

public static class SalesStatuses
{
    public const string Draft = "Draft", Confirmed = "Confirmed", PartiallyFulfilled = "PartiallyFulfilled",
        Fulfilled = "Fulfilled", Cancelled = "Cancelled";
}

public sealed class SalesOrderQuery
{
    public int? WarehouseId { get; set; }
    public string? Search { get; set; }
    public int? CustomerId { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string SortBy { get; set; } = "createdAt";
    public bool Descending { get; set; } = true;
}

public sealed class SalesOrderRequest
{
    [Range(1, int.MaxValue)] public int? WarehouseId { get; set; }
    [Range(1, int.MaxValue)] public int CustomerId { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
    [Required, MinLength(1), MaxLength(100)] public List<SalesItemRequest> Items { get; set; } = [];
}

public sealed class SalesItemRequest
{
    [Range(1, int.MaxValue)] public int ProductId { get; set; }
    [Range(1, int.MaxValue)] public int Quantity { get; set; }
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    [Range(typeof(decimal), "0", "9999999999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal UnitPrice { get; set; }
}

public sealed class SalesFulfillmentRequest
{
    public Guid RequestId { get; set; }
    [StringLength(1000)] public string? Reason { get; set; }
    [Required, MinLength(1), MaxLength(100)] public List<SalesFulfillmentLineRequest> Items { get; set; } = [];
}

public sealed class SalesFulfillmentLineRequest
{
    [Range(1, int.MaxValue)] public int SalesOrderItemId { get; set; }
    [Range(1, int.MaxValue)] public int Quantity { get; set; }
}

public sealed record SalesOrderSummaryDto(int Id, string Code, int CustomerId, string CustomerName,
    string Status, decimal TotalAmount, DateTime CreatedAt, DateTime UpdatedAt, long OrderedQuantity, long FulfilledQuantity, int WarehouseId = 1, string? WarehouseCode = null, string? WarehouseName = null);
public sealed record SalesItemDto(int Id, int ProductId, string Sku, string ProductName,
    int Quantity, int FulfilledQuantity, decimal UnitPrice, decimal LineTotal);
public sealed record SalesFulfillmentLineDto(int SalesOrderItemId, int ProductId, int Quantity, int MovementId);
public sealed record SalesFulfillmentDto(Guid Id, Guid RequestId, DateTime CreatedAt, int CreatedBy,
    IReadOnlyList<SalesFulfillmentLineDto> Items);
public sealed record SalesOrderDetailDto(int Id, string Code, int CustomerId, string CustomerName,
    string Status, decimal TotalAmount, DateTime CreatedAt, DateTime UpdatedAt, long OrderedQuantity,
    long FulfilledQuantity, string? Notes, int CreatedBy, IReadOnlyList<SalesItemDto> Items,
    IReadOnlyList<SalesFulfillmentDto> Fulfillments, int WarehouseId = 1, string? WarehouseCode = null, string? WarehouseName = null);
public sealed record SalesFulfillmentResult(Guid FulfillmentId, Guid RequestId, bool Replayed,
    SalesOrderDetailDto Order, IReadOnlyList<StockOperationDto> Movements);

public interface ISalesOrderService
{
    Task<PagedResult<SalesOrderSummaryDto>> ListAsync(SalesOrderQuery query, CancellationToken cancellationToken);
    Task<SalesOrderDetailDto> GetAsync(int id, CancellationToken cancellationToken);
    Task<SalesOrderDetailDto> CreateAsync(int userId, SalesOrderRequest request, CancellationToken cancellationToken);
    Task<SalesOrderDetailDto> UpdateAsync(int userId, int id, SalesOrderRequest request, CancellationToken cancellationToken);
    Task<SalesOrderDetailDto> ConfirmAsync(int userId, int id, CancellationToken cancellationToken);
    Task<SalesOrderDetailDto> CancelAsync(int userId, int id, CancellationToken cancellationToken);
    Task<SalesFulfillmentResult> FulfillAsync(int userId, int id, SalesFulfillmentRequest request, CancellationToken cancellationToken);
}
