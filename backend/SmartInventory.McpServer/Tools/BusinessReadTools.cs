using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;
using SmartInventory.Application.Customers;
using SmartInventory.Application.Mcp;
using SmartInventory.Application.Purchasing;
using SmartInventory.Application.Reports;
using SmartInventory.Application.Sales;
using SmartInventory.Application.Stock;
using SmartInventory.Application.Suppliers;
using SmartInventory.Application.Warehouses;
using static SmartInventory.McpServer.Tools.InventoryTools;

namespace SmartInventory.McpServer.Tools;

/// <summary>Read-only adapters to completed business modules. Every result comes from shared PostgreSQL services.</summary>
public sealed class BusinessReadTools
{
    private const string Readers = UserRoles.Admin + "," + UserRoles.Staff;

    [Authorize(Roles = Readers)]
    [McpServerTool(Name = "GetSuppliers", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("List suppliers, including inactive records unless isActive is specified. Server-side paging, maximum 100 rows.")]
    public static Task<PagedResult<SupplierDto>> GetSuppliers(ISupplierService suppliers,
        [Description("Optional active/inactive filter.")] bool? isActive = null,
        [Range(1, int.MaxValue)] int page = 1, [Range(1, 100)] int pageSize = 10,
        CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        ValidatePaging(page, pageSize);
        return await suppliers.GetSuppliersAsync(new SupplierQuery { IsActive = isActive, Page = page, PageSize = pageSize }, cancellationToken);
    });

    [Authorize(Roles = Readers)]
    [McpServerTool(Name = "SearchSuppliers", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Search real supplier codes, names and contact fields; includes inactive history unless filtered.")]
    public static Task<PagedResult<SupplierDto>> SearchSuppliers(ISupplierService suppliers,
        [Required, MaxLength(200), Description("Nonempty search text, at most 200 characters.")] string search,
        bool? isActive = null, [Range(1, int.MaxValue)] int page = 1, [Range(1, 100)] int pageSize = 10,
        CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        ValidateSearch(search, true);
        ValidatePaging(page, pageSize);
        return await suppliers.GetSuppliersAsync(new SupplierQuery { Search = search.Trim(), IsActive = isActive, Page = page, PageSize = pageSize }, cancellationToken);
    });

    [Authorize(Roles = Readers)]
    [McpServerTool(Name = "GetCustomers", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("List real customers with optional active/inactive filter and bounded server-side pagination.")]
    public static Task<PagedResult<CustomerDto>> GetCustomers(ICustomerService customers, bool? isActive = null,
        [Range(1, int.MaxValue)] int page = 1, [Range(1, 100)] int pageSize = 10,
        CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        ValidatePaging(page, pageSize);
        return await customers.GetCustomersAsync(new CustomerQuery { IsActive = isActive, Page = page, PageSize = pageSize }, cancellationToken);
    });

    [Authorize(Roles = Readers)]
    [McpServerTool(Name = "SearchCustomers", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Search real customer codes, names and contact fields; includes inactive history unless filtered.")]
    public static Task<PagedResult<CustomerDto>> SearchCustomers(ICustomerService customers,
        [Required, MaxLength(200), Description("Nonempty search text, at most 200 characters.")] string search,
        bool? isActive = null, [Range(1, int.MaxValue)] int page = 1, [Range(1, 100)] int pageSize = 10,
        CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        ValidateSearch(search, true);
        ValidatePaging(page, pageSize);
        return await customers.GetCustomersAsync(new CustomerQuery { Search = search.Trim(), IsActive = isActive, Page = page, PageSize = pageSize }, cancellationToken);
    });

    [Authorize(Roles = Readers)]
    [McpServerTool(Name = "GetPurchaseOrders", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("List real purchase orders, newest first. Filter by supplier, search or Draft/Submitted/Approved/PartiallyReceived/Received/Cancelled status.")]
    public static Task<PagedResult<PurchaseOrderSummaryDto>> GetPurchaseOrders(IPurchaseOrderService orders,
        [MaxLength(200)] string? search = null, [Range(1, int.MaxValue)] int? supplierId = null,
        [MaxLength(32)] string? status = null, [Range(1, int.MaxValue)] int page = 1, [Range(1, 100)] int pageSize = 10,
        [Range(1, int.MaxValue), Description("Optional warehouse scope; omit for aggregate/all warehouses.")] int? warehouseId = null, CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        ValidateSearch(search, false);
        ValidatePaging(page, pageSize);
        return await orders.ListAsync(new PurchaseOrderQuery { WarehouseId = warehouseId, Search = search, SupplierId = supplierId,
            Status = status, Page = page, PageSize = pageSize }, cancellationToken);
    });

    [Authorize(Roles = Readers)]
    [McpServerTool(Name = "GetPurchaseOrderById", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Read a real purchase order with agreed item costs, received counters and receipt history. Does not receive or modify goods.")]
    public static Task<PurchaseOrderDetailDto> GetPurchaseOrderById(IPurchaseOrderService orders,
        [Range(1, int.MaxValue)] int id, CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        PositiveId(id);
        return await orders.GetAsync(id, cancellationToken);
    });

    [Authorize(Roles = Readers)]
    [McpServerTool(Name = "GetPurchaseOrderStatus", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Read the current persisted status and ordered/received quantities of one purchase order.")]
    public static Task<PurchaseOrderStatusDto> GetPurchaseOrderStatus(IPurchaseOrderService orders,
        [Range(1, int.MaxValue)] int id, CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        PositiveId(id);
        var order = await orders.GetAsync(id, cancellationToken);
        return new PurchaseOrderStatusDto(order.Id, order.Code, order.Status, order.OrderedQuantity, order.ReceivedQuantity, order.UpdatedAt, order.WarehouseId, order.WarehouseCode, order.WarehouseName);
    });

    [Authorize(Roles = Readers)]
    [McpServerTool(Name = "GetSalesOrders", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("List real sales orders, newest first. Filter by customer, search or Draft/Confirmed/PartiallyFulfilled/Fulfilled/Cancelled status.")]
    public static Task<PagedResult<SalesOrderSummaryDto>> GetSalesOrders(ISalesOrderService orders,
        [MaxLength(200)] string? search = null, [Range(1, int.MaxValue)] int? customerId = null,
        [MaxLength(32)] string? status = null, [Range(1, int.MaxValue)] int page = 1, [Range(1, 100)] int pageSize = 10,
        [Range(1, int.MaxValue), Description("Optional warehouse scope; omit for aggregate/all warehouses.")] int? warehouseId = null, CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        ValidateSearch(search, false);
        ValidatePaging(page, pageSize);
        return await orders.ListAsync(new SalesOrderQuery { WarehouseId = warehouseId, Search = search, CustomerId = customerId,
            Status = status, Page = page, PageSize = pageSize }, cancellationToken);
    });

    [Authorize(Roles = Readers)]
    [McpServerTool(Name = "GetSalesOrderById", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Read a real sales order with captured item prices, fulfilled counters and fulfillment history. Does not issue or modify stock.")]
    public static Task<SalesOrderDetailDto> GetSalesOrderById(ISalesOrderService orders,
        [Range(1, int.MaxValue)] int id, CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        PositiveId(id);
        return await orders.GetAsync(id, cancellationToken);
    });

    [Authorize(Roles = Readers)]
    [McpServerTool(Name = "GetSalesOrderStatus", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Read the current persisted status and ordered/fulfilled quantities of one sales order.")]
    public static Task<SalesOrderStatusDto> GetSalesOrderStatus(ISalesOrderService orders,
        [Range(1, int.MaxValue)] int id, CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        PositiveId(id);
        var order = await orders.GetAsync(id, cancellationToken);
        return new SalesOrderStatusDto(order.Id, order.Code, order.Status, order.OrderedQuantity, order.FulfilledQuantity, order.UpdatedAt, order.WarehouseId, order.WarehouseCode, order.WarehouseName);
    });

    [Authorize(Roles = Readers)]
    [McpServerTool(Name = "GetStockMovements", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Read actual stock movements with nullable legacy before/after metadata and order references. Inclusive event timestamps; default recent 30 days, maximum 366-day period.")]
    public static Task<PagedResult<StockMovementDetailDto>> GetStockMovements(IStockService stock,
        [Range(1, int.MaxValue)] int? productId = null, [MaxLength(32)] string? type = null,
        [Description("Optional ISO 8601 timestamp with Z or explicit offset.")] string? from = null,
        [Description("Optional ISO 8601 timestamp with Z or explicit offset.")] string? to = null,
        [Range(1, int.MaxValue)] int page = 1, [Range(1, 100)] int pageSize = 10,
        [Range(1, int.MaxValue), Description("Optional warehouse scope; omit for aggregate/all warehouses.")] int? warehouseId = null, CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        ValidatePaging(page, pageSize);
        var period = Period(from, to);
        return await stock.GetMovementsAsync(new StockMovementQuery { WarehouseId = warehouseId, ProductId = productId, Type = type,
            From = period.From!.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            To = period.To!.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), Page = page, PageSize = pageSize }, cancellationToken);
    });

    [Authorize(Roles = Readers)]
    [McpServerTool(Name = "GetPurchaseSummary", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Report real ordered cost by order creation time (all statuses) and received agreed cost by receipt event time, including older orders received in period. Pending count is current. Defaults recent 30 days; maximum 366 days; no profit claim.")]
    public static Task<PurchasePeriodSummaryDto> GetPurchaseSummary(IReportService reports,
        [Description("Optional ISO 8601 timestamp with Z or explicit offset.")] string? from = null,
        [Description("Optional ISO 8601 timestamp with Z or explicit offset.")] string? to = null,
        [Range(1, int.MaxValue), Description("Optional warehouse scope; omit for aggregate/all warehouses.")] int? warehouseId = null, CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        var report = await reports.GetDashboardAsync(Period(from, to, warehouseId), cancellationToken);
        return new PurchasePeriodSummaryDto(report.PeriodFrom, report.PeriodTo, report.PurchaseOrdersValue,
            report.ReceivedPurchaseCost, report.PendingPurchaseOrders, warehouseId, report.Inventory.WarehouseCode, report.Inventory.WarehouseName);
    });

    [Authorize(Roles = Readers)]
    [McpServerTool(Name = "GetSalesSummary", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Report real ordered selling value by order creation time (all statuses) and fulfilled selling value by fulfillment event time, including older orders delivered in period. Pending count is current. Defaults recent 30 days; maximum 366 days; no profit claim.")]
    public static Task<SalesPeriodSummaryDto> GetSalesSummary(IReportService reports,
        [Description("Optional ISO 8601 timestamp with Z or explicit offset.")] string? from = null,
        [Description("Optional ISO 8601 timestamp with Z or explicit offset.")] string? to = null,
        [Range(1, int.MaxValue), Description("Optional warehouse scope; omit for aggregate/all warehouses.")] int? warehouseId = null, CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        var report = await reports.GetDashboardAsync(Period(from, to, warehouseId), cancellationToken);
        return new SalesPeriodSummaryDto(report.PeriodFrom, report.PeriodTo, report.SalesOrdersValue,
            report.FulfilledSalesValue, report.PendingSalesOrders, warehouseId, report.Inventory.WarehouseCode, report.Inventory.WarehouseName);
    });

    [Authorize(Roles = Readers)]
    [McpServerTool(Name = "GetSupplierProducts", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("List products ACTUALLY RECEIVED through this supplier's purchase-order receipts, including inactive history. Sums actual received quantities and agreed receipt costs, not draft orders or an inferred permanent supplier-product relationship.")]
    public static Task<PagedResult<SupplierProductDto>> GetSupplierProducts(IMcpBusinessReadService business,
        [Range(1, int.MaxValue)] int supplierId, [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 10, [Range(1, int.MaxValue), Description("Optional warehouse scope; omit for aggregate/all warehouses.")] int? warehouseId = null, CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        ValidatePaging(page, pageSize);
        PositiveId(supplierId);
        return await business.GetSupplierProductsAsync(supplierId, page, pageSize, cancellationToken, warehouseId);
    });

    [Authorize(Roles = Readers)]
    [McpServerTool(Name = "GetWarehouses", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("List actual warehouses with default MAIN identity, optional active/search filters and bounded paging. Does not change stock.")]
    public static Task<PagedResult<WarehouseDto>> GetWarehouses(IWarehouseService warehouses,
        [MaxLength(200)] string? search = null, bool? isActive = null, [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 10, CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        ValidatePaging(page, pageSize);
        return await warehouses.GetWarehousesAsync(new WarehouseQuery { Search = search, IsActive = isActive, Page = page, PageSize = pageSize }, cancellationToken);
    });

    private static void PositiveId(int id)
    {
        if (id <= 0) throw new AppValidationException("id must be positive.");
    }

    private static void ValidateSearch(string? search, bool required)
    {
        if (search?.Length > 200 || required && string.IsNullOrWhiteSpace(search))
            throw new AppValidationException("search must contain 1 to 200 characters when required, or be omitted.");
    }

    private static ReportPeriodQuery Period(string? from, string? to, int? warehouseId = null)
    {
        var now = DateTimeOffset.UtcNow;
        var start = ParseTimestamp(from, "from") ?? new DateTimeOffset(now.UtcDateTime.Date.AddDays(-29));
        var end = ParseTimestamp(to, "to") ?? now;
        if (start > end || (end - start).TotalDays > 366)
            throw new AppValidationException("from must be before or equal to to; periods cannot exceed 366 days.");
        return new ReportPeriodQuery { From = start, To = end, WarehouseId = warehouseId };
    }

    private static DateTimeOffset? ParseTimestamp(string? value, string field)
    {
        if (value is null) return null;
        if (!Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant) ||
            !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
            throw new AppValidationException($"{field} must be an ISO 8601 timestamp with Z or an explicit offset.");
        return timestamp.ToUniversalTime();
    }
}
