using SmartInventory.Application.Catalog;
using SmartInventory.Application.Stock;
using SmartInventory.Application.Mcp;

namespace SmartInventory.Application.Reports;

public class ReportPeriodQuery
{
    public int? WarehouseId { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
}

public sealed class InventoryReportQuery : ReportPeriodQuery
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public string? StockState { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed record DashboardReportDto(
    InventorySummaryDto Inventory,
    int TotalSuppliers,
    int TotalCustomers,
    int PendingPurchaseOrders,
    int PendingSalesOrders,
    decimal SalesOrdersValue,
    decimal FulfilledSalesValue,
    decimal PurchaseOrdersValue,
    decimal ReceivedPurchaseCost,
    DateTime PeriodFrom,
    DateTime PeriodTo,
    IReadOnlyList<ReportTrendPointDto> Trend);

public sealed record ReportTrendPointDto(DateTime Date, decimal SalesOrdersValue, decimal FulfilledSalesValue,
    decimal PurchaseOrdersValue, decimal ReceivedPurchaseCost);

public sealed record InventoryReportRowDto(int ProductId, string Sku, string ProductName, string CategoryName,
    int Quantity, int MinimumStock, decimal CostPrice, decimal SellingPrice, decimal CostValuation, decimal RetailValuation,
    int? WarehouseId = null, string? WarehouseCode = null, string? WarehouseName = null);

/// <summary>
/// Period activity for an order created in the period or fulfilled in it. Ordered metrics use order creation time;
/// fulfilled metrics use actual fulfillment time. Created orders include every status, including Draft and Cancelled.
/// </summary>
public sealed record SalesReportRowDto(int OrderId, string Code, string CustomerName, string Status,
    DateTime CreatedAt, long OrderedQuantity, long FulfilledQuantity, decimal OrderValue, decimal FulfilledValue,
    int WarehouseId = 1, string? WarehouseCode = null, string? WarehouseName = null);

/// <summary>Period activity: ordered metrics use creation time; received metrics use actual receipt time.</summary>
public sealed record PurchaseReportRowDto(int OrderId, string Code, string SupplierName, string Status,
    DateTime CreatedAt, long OrderedQuantity, long ReceivedQuantity, decimal OrderedCost, decimal ReceivedCost,
    int WarehouseId = 1, string? WarehouseCode = null, string? WarehouseName = null);

public sealed record ProductPerformanceRowDto(int ProductId, string Sku, string ProductName,
    long OrderedQuantity, long FulfilledQuantity, decimal FulfilledSalesValue,
    int? WarehouseId = null, string? WarehouseCode = null, string? WarehouseName = null);

public sealed record StockMovementReportQuery
{
    public int? WarehouseId { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public string? Type { get; set; }
    public int? ProductId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>Filters for a complete, bounded report download. Paging never truncates an export.</summary>
public sealed class ReportExportQuery : ReportPeriodQuery
{
    public string Format { get; set; } = "csv";
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public string? StockState { get; set; }
    public int? ProductId { get; set; }
    public string? Type { get; set; }
    public int Take { get; set; } = 20;
}

public sealed record ReportExportFile(byte[] Content, string ContentType, string FileName);

public interface IReportService
{
    Task<DashboardReportDto> GetDashboardAsync(ReportPeriodQuery query, CancellationToken cancellationToken);
    Task<PagedResult<InventoryReportRowDto>> GetInventoryAsync(InventoryReportQuery query, CancellationToken cancellationToken);
    Task<PagedResult<SalesReportRowDto>> GetSalesAsync(ReportPeriodQuery query, int page, int pageSize, CancellationToken cancellationToken);
    Task<PagedResult<PurchaseReportRowDto>> GetPurchasesAsync(ReportPeriodQuery query, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductPerformanceRowDto>> GetProductPerformanceAsync(ReportPeriodQuery query, int take, CancellationToken cancellationToken);
    Task<PagedResult<StockMovementDetailDto>> GetStockMovementsAsync(StockMovementReportQuery query, CancellationToken cancellationToken);
    Task<string> ExportCsvAsync(string report, InventoryReportQuery query, CancellationToken cancellationToken);
    Task<ReportExportFile> ExportAsync(string report, ReportExportQuery query, CancellationToken cancellationToken);
}
