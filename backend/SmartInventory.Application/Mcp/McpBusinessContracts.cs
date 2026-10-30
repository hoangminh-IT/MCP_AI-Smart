using SmartInventory.Application.Catalog;

namespace SmartInventory.Application.Mcp;

public sealed record SupplierProductDto(int ProductId, string Sku, string ProductName, bool IsActive,
    long ReceivedQuantity, decimal ReceivedCost, DateTime LastReceivedAt, int? WarehouseId = null, string? WarehouseCode = null, string? WarehouseName = null);
public sealed record PurchasePeriodSummaryDto(DateTime PeriodFrom, DateTime PeriodTo,
    decimal OrderedCost, decimal ReceivedCost, int CurrentPendingOrders, int? WarehouseId = null, string? WarehouseCode = null, string? WarehouseName = null);
public sealed record SalesPeriodSummaryDto(DateTime PeriodFrom, DateTime PeriodTo,
    decimal OrderedValue, decimal FulfilledValue, int CurrentPendingOrders, int? WarehouseId = null, string? WarehouseCode = null, string? WarehouseName = null);
public sealed record PurchaseOrderStatusDto(int Id, string Code, string Status, long OrderedQuantity,
    long ReceivedQuantity, DateTime UpdatedAt, int WarehouseId = 1, string? WarehouseCode = null, string? WarehouseName = null);
public sealed record SalesOrderStatusDto(int Id, string Code, string Status, long OrderedQuantity,
    long FulfilledQuantity, DateTime UpdatedAt, int WarehouseId = 1, string? WarehouseCode = null, string? WarehouseName = null);

public interface IMcpBusinessReadService
{
    /// <summary>Products actually received on this supplier's purchase orders, including inactive history.</summary>
    Task<PagedResult<SupplierProductDto>> GetSupplierProductsAsync(int supplierId, int page, int pageSize,
        CancellationToken cancellationToken, int? warehouseId = null);
}
