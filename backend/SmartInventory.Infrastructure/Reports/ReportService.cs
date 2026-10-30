using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;
using SmartInventory.Application.Entities;
using SmartInventory.Application.Mcp;
using SmartInventory.Application.Reports;
using SmartInventory.Application.Stock;
using SmartInventory.Infrastructure.Data;
using SmartInventory.Infrastructure.Mcp;
using SmartInventory.Infrastructure.Catalog;
using SmartInventory.Infrastructure.Warehouses;

namespace SmartInventory.Infrastructure.Reports;

public sealed partial class ReportService(AppDbContext database, IInventoryReadService inventoryRead) : IReportService
{
    private static readonly string[] PendingPurchase = ["Submitted", "Approved", "PartiallyReceived"];
    private static readonly string[] PendingSales = ["Confirmed", "PartiallyFulfilled"];

    public async Task<DashboardReportDto> GetDashboardAsync(ReportPeriodQuery query, CancellationToken cancellationToken)
    {
        var period = ResolvePeriod(query);
        var warehouse = await WarehouseAccess.ReadAsync(database, query.WarehouseId, cancellationToken);
        var stock = await inventoryRead.GetInventorySummaryAsync(cancellationToken, query.WarehouseId);
        var pendingPurchase = await database.PurchaseOrders.CountAsync(order => (!query.WarehouseId.HasValue || order.WarehouseId == query.WarehouseId.Value) && PendingPurchase.Contains(order.Status), cancellationToken);
        var pendingSales = await database.SalesOrders.CountAsync(order => (!query.WarehouseId.HasValue || order.WarehouseId == query.WarehouseId.Value) && PendingSales.Contains(order.Status), cancellationToken);
        var salesOrdersValue = await database.SalesOrders.AsNoTracking()
            .Where(order => (!query.WarehouseId.HasValue || order.WarehouseId == query.WarehouseId.Value) && order.CreatedAt >= period.From && order.CreatedAt <= period.To)
            .SumAsync(order => (decimal?)order.TotalAmount, cancellationToken) ?? 0m;
        var fulfilledSalesValue = await (from fulfillment in database.SalesOrderFulfillments.AsNoTracking()
            join line in database.SalesOrderFulfillmentItems.AsNoTracking() on fulfillment.Id equals line.FulfillmentId
            join orderLine in database.SalesOrderItems.AsNoTracking() on line.SalesOrderItemId equals orderLine.Id
            join order in database.SalesOrders.AsNoTracking() on orderLine.SalesOrderId equals order.Id
            where (!query.WarehouseId.HasValue || order.WarehouseId == query.WarehouseId.Value) && fulfillment.CreatedAt >= period.From && fulfillment.CreatedAt <= period.To
            select orderLine.UnitPrice * line.Quantity).SumAsync(cancellationToken);
        var purchaseOrdersValue = await database.PurchaseOrders.AsNoTracking()
            .Where(order => (!query.WarehouseId.HasValue || order.WarehouseId == query.WarehouseId.Value) && order.CreatedAt >= period.From && order.CreatedAt <= period.To)
            .SumAsync(order => (decimal?)order.TotalAmount, cancellationToken) ?? 0m;
        var receivedPurchaseCost = await (from receipt in database.PurchaseOrderReceipts.AsNoTracking()
            join line in database.PurchaseOrderReceiptItems.AsNoTracking() on receipt.Id equals line.ReceiptId
            join orderLine in database.PurchaseOrderItems.AsNoTracking() on line.PurchaseOrderItemId equals orderLine.Id
            join order in database.PurchaseOrders.AsNoTracking() on orderLine.PurchaseOrderId equals order.Id
            where (!query.WarehouseId.HasValue || order.WarehouseId == query.WarehouseId.Value) && receipt.CreatedAt >= period.From && receipt.CreatedAt <= period.To
            select orderLine.UnitCost * line.Quantity).SumAsync(cancellationToken);
        var supplierCount = await database.Suppliers.CountAsync(cancellationToken);
        var customerCount = await database.Customers.CountAsync(cancellationToken);
        var salesByDay = await database.SalesOrders.AsNoTracking()
            .Where(order => (!query.WarehouseId.HasValue || order.WarehouseId == query.WarehouseId.Value) && order.CreatedAt >= period.From && order.CreatedAt <= period.To)
            .GroupBy(order => order.CreatedAt.Date)
            .Select(group => new { Date = group.Key, Value = group.Sum(order => order.TotalAmount) })
            .ToDictionaryAsync(item => item.Date, item => item.Value, cancellationToken);
        var fulfilledByDay = await (from receipt in database.SalesOrderFulfillments.AsNoTracking()
            join line in database.SalesOrderFulfillmentItems.AsNoTracking() on receipt.Id equals line.FulfillmentId
            join orderLine in database.SalesOrderItems.AsNoTracking() on line.SalesOrderItemId equals orderLine.Id
            join order in database.SalesOrders.AsNoTracking() on orderLine.SalesOrderId equals order.Id
            where (!query.WarehouseId.HasValue || order.WarehouseId == query.WarehouseId.Value) && receipt.CreatedAt >= period.From && receipt.CreatedAt <= period.To
            group orderLine.UnitPrice * line.Quantity by receipt.CreatedAt.Date into grouped
            select new { Date = grouped.Key, Value = grouped.Sum() })
            .ToDictionaryAsync(item => item.Date, item => item.Value, cancellationToken);
        var purchasesByDay = await database.PurchaseOrders.AsNoTracking()
            .Where(order => (!query.WarehouseId.HasValue || order.WarehouseId == query.WarehouseId.Value) && order.CreatedAt >= period.From && order.CreatedAt <= period.To)
            .GroupBy(order => order.CreatedAt.Date)
            .Select(group => new { Date = group.Key, Value = group.Sum(order => order.TotalAmount) })
            .ToDictionaryAsync(item => item.Date, item => item.Value, cancellationToken);
        var receivedByDay = await (from receipt in database.PurchaseOrderReceipts.AsNoTracking()
            join line in database.PurchaseOrderReceiptItems.AsNoTracking() on receipt.Id equals line.ReceiptId
            join orderLine in database.PurchaseOrderItems.AsNoTracking() on line.PurchaseOrderItemId equals orderLine.Id
            join order in database.PurchaseOrders.AsNoTracking() on orderLine.PurchaseOrderId equals order.Id
            where (!query.WarehouseId.HasValue || order.WarehouseId == query.WarehouseId.Value) && receipt.CreatedAt >= period.From && receipt.CreatedAt <= period.To
            group orderLine.UnitCost * line.Quantity by receipt.CreatedAt.Date into grouped
            select new { Date = grouped.Key, Value = grouped.Sum() })
            .ToDictionaryAsync(item => item.Date, item => item.Value, cancellationToken);
        var days = (period.To.Date - period.From.Date).Days + 1;
        var trend = Enumerable.Range(0, days).Select(offset =>
        {
            var date = period.From.Date.AddDays(offset);
            return new ReportTrendPointDto(date, salesByDay.GetValueOrDefault(date), fulfilledByDay.GetValueOrDefault(date),
                purchasesByDay.GetValueOrDefault(date), receivedByDay.GetValueOrDefault(date));
        }).ToArray();
        return new(stock, supplierCount, customerCount, pendingPurchase, pendingSales, salesOrdersValue,
            fulfilledSalesValue, purchaseOrdersValue, receivedPurchaseCost, period.From, period.To, trend);
    }

    public async Task<PagedResult<InventoryReportRowDto>> GetInventoryAsync(InventoryReportQuery query, CancellationToken cancellationToken)
    {
        ValidatePaging(query.Page, query.PageSize);
        if (query.Search?.Length > 200 || query.CategoryId is <= 0)
            throw new AppValidationException("search must be at most 200 characters and categoryId must be positive.");
        var warehouse = await WarehouseAccess.ReadAsync(database, query.WarehouseId, cancellationToken);
        var products = CatalogQueryFactory.Products(database, query.WarehouseId).Where(row => row.IsActive);
        if (query.CategoryId.HasValue) products = products.Where(row => row.CategoryId == query.CategoryId.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            products = products.Where(row => EF.Functions.ILike(row.Name, pattern) || EF.Functions.ILike(row.Sku, pattern));
        }
        products = (query.StockState?.Trim().ToLowerInvariant() ?? "all") switch
        {
            "all" => products,
            "low" => products.Where(row => row.Quantity < row.MinStock),
            "out" => products.Where(row => row.Quantity == 0),
            _ => throw new AppValidationException("stockState must be all, low or out.")
        };
        var count = await products.CountAsync(cancellationToken);
        var rows = await products.OrderBy(row => row.Name).ThenBy(row => row.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(row => new InventoryReportRowDto(row.Id, row.Sku, row.Name, row.CategoryName,
                row.Quantity, row.MinStock, row.CostPrice, row.Price,
                row.CostPrice * row.Quantity, row.Price * row.Quantity,
                query.WarehouseId, warehouse == null ? null : warehouse.Code, warehouse == null ? null : warehouse.Name))
            .ToListAsync(cancellationToken);
        return new(rows, count, query.Page, query.PageSize);
    }

    public async Task<PagedResult<SalesReportRowDto>> GetSalesAsync(ReportPeriodQuery query, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        ValidatePaging(page, pageSize);
        var period = ResolvePeriod(query);
        var warehouse = await WarehouseAccess.ReadAsync(database, query.WarehouseId, cancellationToken);
        var orders = from order in database.SalesOrders.AsNoTracking()
            join customer in database.Customers.AsNoTracking() on order.CustomerId equals customer.Id
            join location in database.Warehouses.AsNoTracking() on order.WarehouseId equals location.Id
            where (!query.WarehouseId.HasValue || order.WarehouseId == query.WarehouseId.Value) &&
                ((order.CreatedAt >= period.From && order.CreatedAt <= period.To) ||
                database.SalesOrderFulfillments.Any(fulfillment => fulfillment.SalesOrderId == order.Id &&
                    fulfillment.CreatedAt >= period.From && fulfillment.CreatedAt <= period.To))
            select new { Order = order, Customer = customer.Name, Warehouse = location };
        var count = await orders.CountAsync(cancellationToken);
        var rows = await orders.OrderByDescending(row => row.Order.CreatedAt).ThenByDescending(row => row.Order.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(row => new SalesReportRowDto(row.Order.Id, row.Order.Code, row.Customer, row.Order.Status,
                row.Order.CreatedAt,
                row.Order.CreatedAt >= period.From && row.Order.CreatedAt <= period.To
                    ? database.SalesOrderItems.Where(line => line.SalesOrderId == row.Order.Id).Sum(line => (long?)line.Quantity) ?? 0 : 0,
                (from fulfillment in database.SalesOrderFulfillments
                 join fulfilled in database.SalesOrderFulfillmentItems on fulfillment.Id equals fulfilled.FulfillmentId
                 where fulfillment.SalesOrderId == row.Order.Id && fulfillment.CreatedAt >= period.From && fulfillment.CreatedAt <= period.To
                 select (long?)fulfilled.Quantity).Sum() ?? 0,
                row.Order.CreatedAt >= period.From && row.Order.CreatedAt <= period.To ? row.Order.TotalAmount : 0m,
                (from fulfillment in database.SalesOrderFulfillments
                 join fulfilled in database.SalesOrderFulfillmentItems on fulfillment.Id equals fulfilled.FulfillmentId
                 join orderLine in database.SalesOrderItems on fulfilled.SalesOrderItemId equals orderLine.Id
                 where fulfillment.SalesOrderId == row.Order.Id && fulfillment.CreatedAt >= period.From && fulfillment.CreatedAt <= period.To
                 select orderLine.UnitPrice * fulfilled.Quantity).Sum(),
                row.Order.WarehouseId, row.Warehouse.Code, row.Warehouse.Name))
            .ToListAsync(cancellationToken);
        return new(rows, count, page, pageSize);
    }

    public async Task<PagedResult<PurchaseReportRowDto>> GetPurchasesAsync(ReportPeriodQuery query, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        ValidatePaging(page, pageSize);
        var period = ResolvePeriod(query);
        var warehouse = await WarehouseAccess.ReadAsync(database, query.WarehouseId, cancellationToken);
        var orders = from order in database.PurchaseOrders.AsNoTracking()
            join supplier in database.Suppliers.AsNoTracking() on order.SupplierId equals supplier.Id
            join location in database.Warehouses.AsNoTracking() on order.WarehouseId equals location.Id
            where (!query.WarehouseId.HasValue || order.WarehouseId == query.WarehouseId.Value) &&
                ((order.CreatedAt >= period.From && order.CreatedAt <= period.To) ||
                database.PurchaseOrderReceipts.Any(receipt => receipt.PurchaseOrderId == order.Id &&
                    receipt.CreatedAt >= period.From && receipt.CreatedAt <= period.To))
            select new { Order = order, Supplier = supplier.Name, Warehouse = location };
        var count = await orders.CountAsync(cancellationToken);
        var rows = await orders.OrderByDescending(row => row.Order.CreatedAt).ThenByDescending(row => row.Order.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(row => new PurchaseReportRowDto(row.Order.Id, row.Order.Code, row.Supplier, row.Order.Status,
                row.Order.CreatedAt,
                row.Order.CreatedAt >= period.From && row.Order.CreatedAt <= period.To
                    ? database.PurchaseOrderItems.Where(line => line.PurchaseOrderId == row.Order.Id).Sum(line => (long?)line.Quantity) ?? 0 : 0,
                (from receipt in database.PurchaseOrderReceipts
                 join receiptLine in database.PurchaseOrderReceiptItems on receipt.Id equals receiptLine.ReceiptId
                 where receipt.PurchaseOrderId == row.Order.Id && receipt.CreatedAt >= period.From && receipt.CreatedAt <= period.To
                 select (long?)receiptLine.Quantity).Sum() ?? 0,
                row.Order.CreatedAt >= period.From && row.Order.CreatedAt <= period.To ? row.Order.TotalAmount : 0m,
                (from receipt in database.PurchaseOrderReceipts
                 join receiptLine in database.PurchaseOrderReceiptItems on receipt.Id equals receiptLine.ReceiptId
                 join orderLine in database.PurchaseOrderItems on receiptLine.PurchaseOrderItemId equals orderLine.Id
                 where receipt.PurchaseOrderId == row.Order.Id && receipt.CreatedAt >= period.From && receipt.CreatedAt <= period.To
                 select orderLine.UnitCost * receiptLine.Quantity).Sum(),
                row.Order.WarehouseId, row.Warehouse.Code, row.Warehouse.Name))
            .ToListAsync(cancellationToken);
        return new(rows, count, page, pageSize);
    }

    public async Task<IReadOnlyList<ProductPerformanceRowDto>> GetProductPerformanceAsync(ReportPeriodQuery query, int take,
        CancellationToken cancellationToken)
    {
        if (take is < 1 or > 100) throw new AppValidationException("take must be between 1 and 100.");
        var period = ResolvePeriod(query);
        var warehouse = await WarehouseAccess.ReadAsync(database, query.WarehouseId, cancellationToken);
        var ordered = from item in database.SalesOrderItems.AsNoTracking()
            join order in database.SalesOrders.AsNoTracking() on item.SalesOrderId equals order.Id
            where (!query.WarehouseId.HasValue || order.WarehouseId == query.WarehouseId.Value) && order.CreatedAt >= period.From && order.CreatedAt <= period.To
            select new { item.ProductId, OrderedQuantity = (long)item.Quantity, FulfilledQuantity = 0L, Value = 0m };
        var fulfilled = from fulfillment in database.SalesOrderFulfillments.AsNoTracking()
            join line in database.SalesOrderFulfillmentItems.AsNoTracking() on fulfillment.Id equals line.FulfillmentId
            join orderLine in database.SalesOrderItems.AsNoTracking() on line.SalesOrderItemId equals orderLine.Id
            join order in database.SalesOrders.AsNoTracking() on orderLine.SalesOrderId equals order.Id
            where (!query.WarehouseId.HasValue || order.WarehouseId == query.WarehouseId.Value) && fulfillment.CreatedAt >= period.From && fulfillment.CreatedAt <= period.To
            select new { orderLine.ProductId, OrderedQuantity = 0L, FulfilledQuantity = (long)line.Quantity, Value = orderLine.UnitPrice * line.Quantity };
        var activity = ordered.Concat(fulfilled).GroupBy(row => row.ProductId)
            .Select(group => new { ProductId = group.Key, OrderedQuantity = group.Sum(row => row.OrderedQuantity),
                FulfilledQuantity = group.Sum(row => row.FulfilledQuantity), Value = group.Sum(row => row.Value) });
        return await (from product in database.Products.AsNoTracking()
            join row in activity on product.Id equals row.ProductId
            orderby row.FulfilledQuantity descending, row.Value descending, product.Id
            select new ProductPerformanceRowDto(product.Id, product.Sku, product.Name,
                row.OrderedQuantity, row.FulfilledQuantity, row.Value, query.WarehouseId,
                warehouse == null ? null : warehouse.Code, warehouse == null ? null : warehouse.Name))
            .Take(take).ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<StockMovementDetailDto>> GetStockMovementsAsync(StockMovementReportQuery query,
        CancellationToken cancellationToken)
    {
        ValidatePaging(query.Page, query.PageSize);
        await WarehouseAccess.ReadAsync(database, query.WarehouseId, cancellationToken);
        var periodQuery = new ReportPeriodQuery { From = query.From, To = query.To, WarehouseId = query.WarehouseId };
        var period = ResolvePeriod(periodQuery);
        if (query.ProductId is <= 0) throw new AppValidationException("productId must be positive when supplied.");
        var movements = database.StockMovements.AsNoTracking().Where(item => item.CreatedAt >= period.From && item.CreatedAt <= period.To);
        if (query.WarehouseId.HasValue) movements = movements.Where(item => item.WarehouseId == query.WarehouseId.Value);
        if (query.ProductId.HasValue) movements = movements.Where(item => item.ProductId == query.ProductId.Value);
        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            var type = query.Type.Trim();
            if (type.Length > 32) throw new AppValidationException("type must be at most 32 characters.");
            movements = movements.Where(item => item.Type.ToLower() == type.ToLower());
        }
        var count = await movements.CountAsync(cancellationToken);
        var rows = await (from movement in movements
            join product in database.Products.AsNoTracking() on movement.ProductId equals product.Id
            join location in database.Warehouses.AsNoTracking() on movement.WarehouseId equals location.Id
            orderby movement.CreatedAt descending, movement.Id descending
            select new StockMovementDetailDto(movement.Id, product.Id, product.Sku, product.Name, movement.Type,
                movement.Quantity, movement.QuantityBefore, movement.QuantityAfter, movement.Reason,
                movement.CreatedBy, movement.CreatedAt, movement.RequestId, movement.ReferenceType, movement.ReferenceId,
                movement.WarehouseId, location.Code, location.Name, movement.WarehouseQuantityBefore, movement.WarehouseQuantityAfter))
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return new(rows, count, query.Page, query.PageSize);
    }

    private static (DateTime From, DateTime To) ResolvePeriod(ReportPeriodQuery query)
    {
        var now = DateTime.UtcNow;
        var from = query.From?.UtcDateTime ?? now.Date.AddDays(-29);
        var to = query.To?.UtcDateTime ?? now;
        if (from > to) throw new AppValidationException("from must be before or equal to to.");
        if ((to - from).TotalDays > 366) throw new AppValidationException("Report periods cannot exceed 366 days.");
        return (from, to);
    }

    private static void ValidatePaging(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new AppValidationException("page must be positive and pageSize must be between 1 and 100.");
    }

}
