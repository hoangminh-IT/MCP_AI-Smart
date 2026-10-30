using System.Data;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;
using SmartInventory.Application.Reports;
using SmartInventory.Infrastructure.Warehouses;

namespace SmartInventory.Infrastructure.Reports;

public sealed partial class ReportService
{
    public async Task<string> ExportCsvAsync(string report, InventoryReportQuery query, CancellationToken cancellationToken)
    {
        var file = await ExportAsync(report, new ReportExportQuery { From = query.From, To = query.To, WarehouseId = query.WarehouseId,
            Search = query.Search, CategoryId = query.CategoryId, StockState = query.StockState }, cancellationToken);
        // Retain the existing application contract: the download endpoint adds the UTF-8 BOM.
        return Encoding.UTF8.GetString(file.Content.AsSpan(Encoding.UTF8.Preamble.Length));
    }

    public async Task<ReportExportFile> ExportAsync(string report, ReportExportQuery query, CancellationToken cancellationToken)
    {
        var name = report.Trim().ToLowerInvariant();
        var format = query.Format?.Trim().ToLowerInvariant() ?? "csv";
        if (name is not ("dashboard" or "inventory" or "sales" or "purchases" or "stock-movements" or "product-performance"))
            throw new AppValidationException("report must be dashboard, inventory, sales, purchases, stock-movements or product-performance.");
        if (format is not ("csv" or "xlsx" or "pdf"))
            throw new AppValidationException("format must be csv, xlsx or pdf.");
        var limit = format == "pdf" ? 1000 : 10000;
        // Resolve defaults once, so every page and format uses exactly the same inclusive UTC bounds.
        var period = name == "inventory" ? ((DateTime From, DateTime To)?)null : ResolvePeriod(query);
        var periodQuery = period.HasValue
            ? new ReportPeriodQuery { From = period.Value.From, To = period.Value.To, WarehouseId = query.WarehouseId }
            : new ReportPeriodQuery { WarehouseId = query.WarehouseId };
        var generatedAt = DateTime.UtcNow;
        if (database.Database.CurrentTransaction is { } existing &&
            existing.GetDbTransaction().IsolationLevel is not (IsolationLevel.RepeatableRead or IsolationLevel.Serializable))
            throw new AppConflictException("A consistent report export requires repeatable-read transaction isolation.");
        ExportTable table;
        string company;
        string scope;
        // Only database reads occur inside this snapshot. CPU rendering does not hold its transaction open.
        await using (var transaction = database.Database.CurrentTransaction is null
            ? await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken) : null)
        {
            var warehouse = await WarehouseAccess.ReadAsync(database, query.WarehouseId, cancellationToken);
            scope = warehouse == null ? "All warehouses (aggregate)" : $"{warehouse.Code} — {warehouse.Name} (ID {warehouse.Id})";
            company = await database.ApplicationSettings.AsNoTracking().Where(settings => settings.Id == 1)
                .Select(settings => settings.CompanyName).SingleOrDefaultAsync(cancellationToken) ?? "AI Smart Inventory";
            table = await ReadExportTableAsync(name, query, periodQuery, limit, cancellationToken);
            // Scoped CSV includes the warehouse too; XLSX/PDF also retain it in their context sheet/header.
            if (query.WarehouseId.HasValue && name is "inventory" or "product-performance" or "dashboard")
                table = new ExportTable([.. table.Headers, "WarehouseId", "Warehouse"],
                    table.Rows.Select(row => (object?[])[.. row, query.WarehouseId.Value, scope]).ToArray());
            if (table.Rows.Count > limit) throw RowLimit(limit);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        var context = new List<KeyValuePair<string, string>>
        {
            new("Company", company), new("Report", name), new("Warehouse", scope),
            new("ScopeRules", "Product/category/supplier/customer master counts are global. Quantity, valuation, movements and order activity follow the selected warehouse; local low stock uses each product minimum. Notifications use aggregate stock."), new("GeneratedAtUtc", ReportFileRenderer.Text(generatedAt)),
            new("Rows", table.Rows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("Basis", name == "inventory" ? "Current active-product inventory snapshot; date filters do not apply."
                : "Inclusive UTC period. Ordered values use order creation; received/fulfilled values use actual event time. No revenue is inferred."),
            new("Precision", "Decimal amounts are exact. XLSX values that exceed Excel numeric precision are stored as text."),
            new("NullValues", "Blank before/after quantities on legacy movements mean unknown, not zero.")
        };
        if (period.HasValue)
        {
            context.Add(new("FromUtc", ReportFileRenderer.Text(period.Value.From)));
            context.Add(new("ToUtc", ReportFileRenderer.Text(period.Value.To)));
        }
        if (name == "inventory")
        {
            context.Add(new("Search", query.Search?.Trim() ?? ""));
            context.Add(new("CategoryId", query.CategoryId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "all"));
            context.Add(new("StockState", query.StockState?.Trim().ToLowerInvariant() ?? "all"));
        }
        if (name == "stock-movements")
        {
            context.Add(new("ProductId", query.ProductId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "all"));
            context.Add(new("Type", query.Type?.Trim() ?? "all"));
        }
        if (name == "product-performance") context.Add(new("Take", query.Take.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        cancellationToken.ThrowIfCancellationRequested();
        var content = format switch
        {
            "csv" => ReportFileRenderer.Csv(table, cancellationToken),
            "xlsx" => ReportFileRenderer.Xlsx(table, context, cancellationToken),
            _ => ReportFileRenderer.Pdf(table, context, cancellationToken)
        };
        cancellationToken.ThrowIfCancellationRequested();
        return new(content, format switch { "csv" => "text/csv; charset=utf-8",
            "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", _ => "application/pdf" },
            $"smart-inventory-{name}.{format}");
    }

    private async Task<ExportTable> ReadExportTableAsync(string report, ReportExportQuery query,
        ReportPeriodQuery period, int limit, CancellationToken cancellationToken)
    {
        switch (report)
        {
            case "inventory":
                var inventory = await ReadAllAsync((page, size) => GetInventoryAsync(new InventoryReportQuery
                    { Search = query.Search, CategoryId = query.CategoryId, StockState = query.StockState, WarehouseId = query.WarehouseId,
                        Page = page, PageSize = size }, cancellationToken), limit, cancellationToken);
                return new(["Sku", "Product", "Category", "Quantity", "MinimumStock", "CostPrice", "SellingPrice", "CostValuation", "RetailValuation"],
                    inventory.Select(row => new object?[] { row.Sku, row.ProductName, row.CategoryName, row.Quantity,
                        row.MinimumStock, row.CostPrice, row.SellingPrice, row.CostValuation, row.RetailValuation }).ToArray());
            case "sales":
                var sales = await ReadAllAsync((page, size) => GetSalesAsync(period, page, size, cancellationToken), limit, cancellationToken);
                return new(["Code", "Customer", "Status", "CreatedAtUtc", "OrderedQuantity", "FulfilledQuantity", "SalesOrderValue", "FulfilledSalesValue", "WarehouseId", "WarehouseCode", "WarehouseName"],
                    sales.Select(row => new object?[] { row.Code, row.CustomerName, row.Status, row.CreatedAt,
                        row.OrderedQuantity, row.FulfilledQuantity, row.OrderValue, row.FulfilledValue, row.WarehouseId, row.WarehouseCode, row.WarehouseName }).ToArray());
            case "purchases":
                var purchases = await ReadAllAsync((page, size) => GetPurchasesAsync(period, page, size, cancellationToken), limit, cancellationToken);
                return new(["Code", "Supplier", "Status", "CreatedAtUtc", "OrderedQuantity", "ReceivedQuantity", "OrderedCost", "ReceivedCost", "WarehouseId", "WarehouseCode", "WarehouseName"],
                    purchases.Select(row => new object?[] { row.Code, row.SupplierName, row.Status, row.CreatedAt,
                        row.OrderedQuantity, row.ReceivedQuantity, row.OrderedCost, row.ReceivedCost, row.WarehouseId, row.WarehouseCode, row.WarehouseName }).ToArray());
            case "stock-movements":
                var movements = await ReadAllAsync((page, size) => GetStockMovementsAsync(new StockMovementReportQuery
                    { From = period.From, To = period.To, ProductId = query.ProductId, Type = query.Type, WarehouseId = query.WarehouseId,
                        Page = page, PageSize = size }, cancellationToken), limit, cancellationToken);
                return new(["Id", "ProductId", "Sku", "Product", "Type", "Quantity", "QuantityBefore", "QuantityAfter", "Reason", "CreatedBy", "CreatedAtUtc", "RequestId", "ReferenceType", "ReferenceId", "WarehouseId", "WarehouseCode", "WarehouseName", "WarehouseQuantityBefore", "WarehouseQuantityAfter"],
                    movements.Select(row => new object?[] { row.Id, row.ProductId, row.Sku, row.ProductName,
                        row.Type, row.Quantity, row.QuantityBefore, row.QuantityAfter, row.Reason, row.CreatedBy,
                        row.CreatedAt, row.RequestId, row.ReferenceType, row.ReferenceId, row.WarehouseId, row.WarehouseCode, row.WarehouseName,
                        row.WarehouseQuantityBefore, row.WarehouseQuantityAfter }).ToArray());
            case "product-performance":
                var performance = await GetProductPerformanceAsync(period, query.Take, cancellationToken);
                return new(["ProductId", "Sku", "Product", "OrderedQuantity", "FulfilledQuantity", "FulfilledSalesValue"],
                    performance.Select(row => new object?[] { row.ProductId, row.Sku, row.ProductName,
                        row.OrderedQuantity, row.FulfilledQuantity, row.FulfilledSalesValue }).ToArray());
            default:
                var dashboard = await GetDashboardAsync(period, cancellationToken);
                List<object?[]> rows =
                [
                    ["TotalProducts", null, dashboard.Inventory.TotalProducts],
                    ["TotalCategories", null, dashboard.Inventory.TotalCategories],
                    ["TotalQuantity", null, dashboard.Inventory.TotalQuantity],
                    ["InventoryCostValue", null, dashboard.Inventory.InventoryCostValue],
                    ["InventoryRetailValue", null, dashboard.Inventory.InventoryRetailValue],
                    ["LowStockCount", null, dashboard.Inventory.LowStockCount],
                    ["OutOfStockCount", null, dashboard.Inventory.OutOfStockCount],
                    ["StockMovementCount", null, dashboard.Inventory.StockMovementCount],
                    ["TotalSuppliers", null, dashboard.TotalSuppliers],
                    ["TotalCustomers", null, dashboard.TotalCustomers],
                    ["PendingPurchaseOrders", null, dashboard.PendingPurchaseOrders],
                    ["PendingSalesOrders", null, dashboard.PendingSalesOrders],
                    ["SalesOrdersValue", null, dashboard.SalesOrdersValue],
                    ["FulfilledSalesValue", null, dashboard.FulfilledSalesValue],
                    ["PurchaseOrdersValue", null, dashboard.PurchaseOrdersValue],
                    ["ReceivedPurchaseCost", null, dashboard.ReceivedPurchaseCost]
                ];
                foreach (var day in dashboard.Trend)
                {
                    rows.Add(["Trend.SalesOrdersValue", day.Date, day.SalesOrdersValue]);
                    rows.Add(["Trend.FulfilledSalesValue", day.Date, day.FulfilledSalesValue]);
                    rows.Add(["Trend.PurchaseOrdersValue", day.Date, day.PurchaseOrdersValue]);
                    rows.Add(["Trend.ReceivedPurchaseCost", day.Date, day.ReceivedPurchaseCost]);
                }
                return new(["Metric", "DateUtc", "Value"], rows);
        }
    }

    private static async Task<IReadOnlyList<T>> ReadAllAsync<T>(Func<int, int, Task<PagedResult<T>>> readPage,
        int limit, CancellationToken cancellationToken)
    {
        const int pageSize = 100;
        var first = await readPage(1, pageSize);
        if (first.TotalCount > limit) throw RowLimit(limit);
        if (first.Items.Count != Math.Min(first.TotalCount, pageSize))
            throw new AppConflictException("Report changed while exporting. Retry the export.");
        var rows = new List<T>(first.Items);
        var pageCount = (first.TotalCount + pageSize - 1) / pageSize;
        for (var page = 2; page <= pageCount; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var next = await readPage(page, pageSize);
            if (next.TotalCount != first.TotalCount || next.Items.Count != Math.Min(pageSize, first.TotalCount - rows.Count))
                throw new AppConflictException("Report changed while exporting. Retry the export.");
            rows.AddRange(next.Items);
        }
        return rows;
    }

    private static AppValidationException RowLimit(int limit) =>
        new($"This export is limited to {limit:N0} rows. Narrow the report filters or date range before exporting.");
}

internal sealed record ExportTable(IReadOnlyList<string> Headers, IReadOnlyList<object?[]> Rows);
