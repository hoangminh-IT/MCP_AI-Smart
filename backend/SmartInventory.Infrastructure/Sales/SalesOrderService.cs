using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;
using SmartInventory.Application.Entities;
using SmartInventory.Application.Sales;
using SmartInventory.Application.Notifications;
using SmartInventory.Application.Stock;
using SmartInventory.Infrastructure.Data;
using SmartInventory.Infrastructure.Warehouses;

namespace SmartInventory.Infrastructure.Sales;

public sealed class SalesOrderService(AppDbContext database, IStockTransactionService stock,
    ILogger<SalesOrderService> logger, INotificationService notifications) : ISalesOrderService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] Statuses = [SalesStatuses.Draft, SalesStatuses.Confirmed,
        SalesStatuses.PartiallyFulfilled, SalesStatuses.Fulfilled, SalesStatuses.Cancelled];

    public async Task<PagedResult<SalesOrderSummaryDto>> ListAsync(SalesOrderQuery query, CancellationToken cancellationToken)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue ||
            query.CustomerId is <= 0 || query.Search?.Length > 200)
            throw new AppValidationException("Use positive pagination, pageSize up to 100, a positive customerId and search up to 200 characters.");
        await WarehouseAccess.ReadAsync(database, query.WarehouseId, cancellationToken);
        var orders = from order in database.SalesOrders.AsNoTracking()
            join customer in database.Customers.AsNoTracking() on order.CustomerId equals customer.Id
            join warehouse in database.Warehouses.AsNoTracking() on order.WarehouseId equals warehouse.Id
            select new { Order = order, CustomerName = customer.Name, WarehouseCode = warehouse.Code, WarehouseName = warehouse.Name };
        if (query.WarehouseId.HasValue) orders = orders.Where(row => row.Order.WarehouseId == query.WarehouseId.Value);
        if (query.CustomerId.HasValue) orders = orders.Where(row => row.Order.CustomerId == query.CustomerId.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            orders = orders.Where(row => EF.Functions.ILike(row.Order.Code, pattern) || EF.Functions.ILike(row.CustomerName, pattern));
        }
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = Statuses.SingleOrDefault(value => value.Equals(query.Status.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new AppValidationException("Unknown sales order status.");
            orders = orders.Where(row => row.Order.Status == status);
        }
        var sorted = (query.SortBy?.Trim().ToLowerInvariant() ?? "createdat") switch
        {
            "createdat" => query.Descending ? orders.OrderByDescending(row => row.Order.CreatedAt) : orders.OrderBy(row => row.Order.CreatedAt),
            "updatedat" => query.Descending ? orders.OrderByDescending(row => row.Order.UpdatedAt) : orders.OrderBy(row => row.Order.UpdatedAt),
            "code" => query.Descending ? orders.OrderByDescending(row => row.Order.Code) : orders.OrderBy(row => row.Order.Code),
            "customername" => query.Descending ? orders.OrderByDescending(row => row.CustomerName) : orders.OrderBy(row => row.CustomerName),
            "status" => query.Descending ? orders.OrderByDescending(row => row.Order.Status) : orders.OrderBy(row => row.Order.Status),
            "totalamount" => query.Descending ? orders.OrderByDescending(row => row.Order.TotalAmount) : orders.OrderBy(row => row.Order.TotalAmount),
            _ => throw new AppValidationException("Unsupported sales order sortBy.")
        };
        var count = await orders.CountAsync(cancellationToken);
        var items = await sorted.ThenBy(row => row.Order.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(row => new SalesOrderSummaryDto(row.Order.Id, row.Order.Code, row.Order.CustomerId, row.CustomerName,
                row.Order.Status, row.Order.TotalAmount, row.Order.CreatedAt, row.Order.UpdatedAt,
                database.SalesOrderItems.Where(item => item.SalesOrderId == row.Order.Id).Sum(item => (long?)item.Quantity) ?? 0,
                database.SalesOrderItems.Where(item => item.SalesOrderId == row.Order.Id).Sum(item => (long?)item.FulfilledQuantity) ?? 0, row.Order.WarehouseId, row.WarehouseCode, row.WarehouseName))
            .ToListAsync(cancellationToken);
        return new(items, count, query.Page, query.PageSize);
    }

    public async Task<SalesOrderDetailDto> GetAsync(int id, CancellationToken cancellationToken)
    {
        var header = await (from order in database.SalesOrders.AsNoTracking()
            join customer in database.Customers.AsNoTracking() on order.CustomerId equals customer.Id
            join warehouse in database.Warehouses.AsNoTracking() on order.WarehouseId equals warehouse.Id
            where order.Id == id
            select new { Order = order, CustomerName = customer.Name, WarehouseCode = warehouse.Code, WarehouseName = warehouse.Name }).SingleOrDefaultAsync(cancellationToken)
            ?? throw new AppNotFoundException("Sales order was not found.");
        var items = await (from item in database.SalesOrderItems.AsNoTracking()
            join product in database.Products.AsNoTracking() on item.ProductId equals product.Id
            where item.SalesOrderId == id orderby item.Id
            select new SalesItemDto(item.Id, product.Id, product.Sku, product.Name, item.Quantity,
                item.FulfilledQuantity, item.UnitPrice, item.UnitPrice * item.Quantity)).ToListAsync(cancellationToken);
        var fulfillments = await database.SalesOrderFulfillments.AsNoTracking().Where(item => item.SalesOrderId == id)
            .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id).ToListAsync(cancellationToken);
        var fulfillmentLines = await (from line in database.SalesOrderFulfillmentItems.AsNoTracking()
            join fulfillment in database.SalesOrderFulfillments.AsNoTracking() on line.FulfillmentId equals fulfillment.Id
            where fulfillment.SalesOrderId == id orderby line.Id
            select new { line.FulfillmentId, Line = new SalesFulfillmentLineDto(line.SalesOrderItemId, line.ProductId, line.Quantity, line.MovementId) })
            .ToListAsync(cancellationToken);
        var linesByFulfillment = fulfillmentLines.ToLookup(line => line.FulfillmentId, line => line.Line);
        return new(header.Order.Id, header.Order.Code, header.Order.CustomerId, header.CustomerName,
            header.Order.Status, header.Order.TotalAmount, header.Order.CreatedAt, header.Order.UpdatedAt,
            items.Sum(item => (long)item.Quantity), items.Sum(item => (long)item.FulfilledQuantity), header.Order.Notes,
            header.Order.CreatedBy, items, fulfillments.Select(item => new SalesFulfillmentDto(item.Id, item.RequestId,
                item.CreatedAt, item.CreatedBy, linesByFulfillment[item.Id].ToList())).ToList(),
            header.Order.WarehouseId, header.WarehouseCode, header.WarehouseName);
    }

    public async Task<SalesOrderDetailDto> CreateAsync(int userId, SalesOrderRequest request, CancellationToken cancellationToken)
    {
        var total = SalesRules.ValidateOrder(request);
        await ValidateActorAsync(userId, cancellationToken);
        await ValidateActiveReferencesAsync(request.CustomerId, request.Items.Select(item => item.ProductId).ToArray(), cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var warehouseId = request.WarehouseId ?? WarehouseAccess.MainId;
        await WarehouseAccess.LockActiveAsync(database, [warehouseId], cancellationToken);
        var order = new SalesOrder
        {
            WarehouseId = warehouseId, Code = "SO-" + Guid.NewGuid().ToString("N")[..16].ToUpperInvariant(), CustomerId = request.CustomerId,
            Notes = request.Notes, TotalAmount = total, CreatedBy = userId
        };
        database.SalesOrders.Add(order);
        await database.SaveChangesAsync(cancellationToken);
        database.SalesOrderItems.AddRange(request.Items.Select(item => new SalesOrderItem
        {
            SalesOrderId = order.Id, ProductId = item.ProductId, Quantity = item.Quantity, UnitPrice = item.UnitPrice
        }));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(order.Id, cancellationToken);
    }

    public async Task<SalesOrderDetailDto> UpdateAsync(int userId, int id, SalesOrderRequest request, CancellationToken cancellationToken)
    {
        var total = SalesRules.ValidateOrder(request);
        await ValidateActorAsync(userId, cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var order = await LockOrderAsync(id, cancellationToken);
        if (order.Status != SalesStatuses.Draft) throw new AppConflictException("Only draft sales orders can be edited.");
        var warehouseId = request.WarehouseId ?? order.WarehouseId;
        await WarehouseAccess.LockActiveAsync(database, [warehouseId], cancellationToken);
        order.WarehouseId = warehouseId;
        await ValidateActiveReferencesAsync(request.CustomerId, request.Items.Select(item => item.ProductId).ToArray(), cancellationToken);
        await database.SalesOrderItems.Where(item => item.SalesOrderId == id).ExecuteDeleteAsync(cancellationToken);
        database.SalesOrderItems.AddRange(request.Items.Select(item => new SalesOrderItem
        {
            SalesOrderId = id, ProductId = item.ProductId, Quantity = item.Quantity, UnitPrice = item.UnitPrice
        }));
        order.CustomerId = request.CustomerId;
        order.Notes = request.Notes;
        order.TotalAmount = total;
        order.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<SalesOrderDetailDto> ConfirmAsync(int userId, int id, CancellationToken cancellationToken)
    {
        await ValidateActorAsync(userId, cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var order = await LockOrderAsync(id, cancellationToken);
        if (order.Status != SalesStatuses.Draft) throw new AppConflictException("Only draft sales orders can be confirmed.");
        var productIds = await database.SalesOrderItems.Where(item => item.SalesOrderId == id)
            .Select(item => item.ProductId).ToArrayAsync(cancellationToken);
        await WarehouseAccess.LockActiveAsync(database, [order.WarehouseId], cancellationToken);
        await ValidateActiveReferencesAsync(order.CustomerId, productIds, cancellationToken);
        order.Status = SalesStatuses.Confirmed;
        order.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        await notifications.PublishAsync($"sales-order:{id}:Confirmed", "SalesOrder",
            "Đơn bán hàng đã xác nhận", $"Đơn {order.Code} đã được xác nhận và đang chờ giao.",
            "SalesOrder", id.ToString(CultureInfo.InvariantCulture), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<SalesOrderDetailDto> CancelAsync(int userId, int id, CancellationToken cancellationToken)
    {
        await ValidateActorAsync(userId, cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var order = await LockOrderAsync(id, cancellationToken);
        if (order.Status != SalesStatuses.Cancelled)
        {
            if (order.Status is not (SalesStatuses.Draft or SalesStatuses.Confirmed))
                throw new AppConflictException("A sales order can be cancelled only before any goods are fulfilled.");
            if (await database.SalesOrderFulfillments.AnyAsync(item => item.SalesOrderId == id, cancellationToken))
                throw new AppConflictException("A sales order with a fulfillment record cannot be cancelled.");
            order.Status = SalesStatuses.Cancelled;
            order.UpdatedAt = DateTime.UtcNow;
            await database.SaveChangesAsync(cancellationToken);
        }
        await notifications.PublishAsync($"sales-order:{id}:Cancelled", "SalesOrder",
            "Đơn bán hàng đã hủy", $"Đơn {order.Code} đã được hủy trước khi giao hàng.",
            "SalesOrder", id.ToString(CultureInfo.InvariantCulture), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<SalesFulfillmentResult> FulfillAsync(int userId, int id, SalesFulfillmentRequest request,
        CancellationToken cancellationToken)
    {
        SalesRules.ValidateFulfillment(request);
        await ValidateActorAsync(userId, cancellationToken);
        var fingerprint = Fingerprint(userId, id, request);
        var existing = await FindFulfillmentAsync(request.RequestId, cancellationToken);
        if (existing is not null) return Replay(existing, userId, id, fingerprint);

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var order = await LockOrderAsync(id, cancellationToken);
            existing = await FindFulfillmentAsync(request.RequestId, cancellationToken);
            if (existing is not null)
            {
                var replay = Replay(existing, userId, id, fingerprint);
                await transaction.CommitAsync(cancellationToken);
                return replay;
            }
            if (order.Status is not (SalesStatuses.Confirmed or SalesStatuses.PartiallyFulfilled))
                throw new AppConflictException("Only confirmed or partially fulfilled sales orders can be fulfilled.");
            var orderItems = await database.SalesOrderItems.Where(item => item.SalesOrderId == id).ToListAsync(cancellationToken);
            var byId = orderItems.ToDictionary(item => item.Id);
            foreach (var line in request.Items)
            {
                if (!byId.TryGetValue(line.SalesOrderItemId, out var item))
                    throw new AppValidationException("A fulfillment line does not belong to this sales order.");
                if (line.Quantity > item.Quantity - item.FulfilledQuantity)
                    throw new AppConflictException("A fulfillment quantity exceeds the order line's remaining quantity.");
            }
            var customer = await database.Customers.FromSqlInterpolated(
                $"SELECT * FROM customers WHERE id = {order.CustomerId} FOR SHARE").SingleOrDefaultAsync(cancellationToken);
            if (customer is null || !customer.IsActive) throw new AppConflictException("The customer must be active to fulfill the sales order.");
            await WarehouseAccess.LockActiveAsync(database, [order.WarehouseId], cancellationToken);
            var productIds = request.Items.Select(line => byId[line.SalesOrderItemId].ProductId).Order().ToArray();
            var products = await database.Products.FromSqlInterpolated(
                $"SELECT * FROM products WHERE id = ANY ({productIds}) ORDER BY id FOR UPDATE").ToListAsync(cancellationToken);
            var inventories = await database.Inventories.FromSqlInterpolated(
                $"SELECT * FROM inventory WHERE product_id = ANY ({productIds}) ORDER BY product_id FOR UPDATE").ToListAsync(cancellationToken);
            var stockByProduct = inventories.ToDictionary(item => item.ProductId);
            if (products.Count != productIds.Length || products.Any(product => !product.IsActive) || inventories.Count != productIds.Length)
                throw new AppConflictException("Every fulfilled product must be active and have an inventory record.");
            var localStocks = new Dictionary<int, WarehouseInventory>();
            foreach (var productId in productIds)
                localStocks[productId] = await WarehouseAccess.LockBalanceAsync(database, productId, order.WarehouseId, cancellationToken);
            foreach (var line in request.Items)
            {
                var item = byId[line.SalesOrderItemId];
                var available = localStocks[item.ProductId].Quantity;
                if (available < line.Quantity)
                    throw new AppConflictException($"Insufficient stock for product {item.ProductId}; requested fulfillment exceeds available quantity.");
            }

            var fulfillment = new SalesOrderFulfillment
            {
                SalesOrderId = id, RequestId = request.RequestId, CreatedBy = userId, PayloadFingerprint = fingerprint,
                Reason = request.Reason ?? $"Sales order {order.Code} fulfillment"
            };
            database.SalesOrderFulfillments.Add(fulfillment);
            await database.SaveChangesAsync(cancellationToken);
            await notifications.PublishAsync($"sales-fulfillment:{fulfillment.Id:N}", "SalesOrder",
                "Đơn bán hàng đã giao", $"Đơn {order.Code} đã ghi nhận một lần giao hàng.", "SalesOrder",
                id.ToString(CultureInfo.InvariantCulture), cancellationToken);
            var movements = new List<StockOperationDto>();
            foreach (var line in request.Items.OrderBy(line => byId[line.SalesOrderItemId].ProductId))
            {
                var item = byId[line.SalesOrderItemId];
                var movement = await stock.IssueInTransactionAsync(userId, new StockRequest
                {
                    WarehouseId = order.WarehouseId, ProductId = item.ProductId, Quantity = line.Quantity, Reason = fulfillment.Reason,
                    RequestId = MovementRequestId(request.RequestId, id, item.Id)
                }, "SalesOrder", id.ToString(CultureInfo.InvariantCulture), cancellationToken);
                movements.Add(movement);
                database.SalesOrderFulfillmentItems.Add(new SalesOrderFulfillmentItem
                {
                    FulfillmentId = fulfillment.Id, SalesOrderItemId = item.Id, ProductId = item.ProductId,
                    Quantity = line.Quantity, MovementId = movement.MovementId
                });
                item.FulfilledQuantity += line.Quantity;
            }
            order.Status = orderItems.All(item => item.FulfilledQuantity == item.Quantity)
                ? SalesStatuses.Fulfilled : SalesStatuses.PartiallyFulfilled;
            order.UpdatedAt = DateTime.UtcNow;
            await database.SaveChangesAsync(cancellationToken);
            var response = new SalesFulfillmentResult(fulfillment.Id, fulfillment.RequestId, false,
                await GetAsync(id, cancellationToken), movements);
            fulfillment.ResponseSnapshot = JsonSerializer.Serialize(response, JsonOptions);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation("Sales order {SalesOrderId} fulfilled by user {UserId}; request {RequestId}, lines {LineCount}.",
                id, userId, request.RequestId, request.Items.Count);
            return response;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_sales_order_fulfillments_request_id" })
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            existing = await FindFulfillmentAsync(request.RequestId, cancellationToken);
            if (existing is null) throw new AppConflictException("The fulfillment request is being processed. Retry with the same request ID.");
            return Replay(existing, userId, id, fingerprint);
        }
    }

    private Task<SalesOrder?> LoadOrderAsync(int id, CancellationToken cancellationToken) => database.SalesOrders
        .FromSqlInterpolated($"SELECT * FROM sales_orders WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
    private async Task<SalesOrder> LockOrderAsync(int id, CancellationToken cancellationToken) =>
        await LoadOrderAsync(id, cancellationToken) ?? throw new AppNotFoundException("Sales order was not found.");
    private Task<SalesOrderFulfillment?> FindFulfillmentAsync(Guid requestId, CancellationToken cancellationToken) =>
        database.SalesOrderFulfillments.AsNoTracking().SingleOrDefaultAsync(item => item.RequestId == requestId, cancellationToken);

    private async Task ValidateActorAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null || !user.IsActive || !UserRoles.IsValid(user.Role))
            throw new AppUnauthorizedException("The account is no longer available.");
        if (!UserRoles.CanManageInventory(user.Role))
            throw new AppForbiddenException("An inventory operator account is required.");
    }

    private async Task ValidateActiveReferencesAsync(int customerId, int[] productIds, CancellationToken cancellationToken)
    {
        if (!await database.Customers.AnyAsync(customer => customer.Id == customerId && customer.IsActive, cancellationToken))
            throw new AppConflictException("Select an active customer.");
        if (productIds.Length == 0 || await database.Products.CountAsync(product => productIds.Contains(product.Id) && product.IsActive, cancellationToken) != productIds.Length)
            throw new AppConflictException("Every order product must be active.");
    }

    private static string Fingerprint(int userId, int orderId, SalesFulfillmentRequest request) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            userId, orderId, reason = request.Reason ?? string.Empty,
            items = request.Items.OrderBy(item => item.SalesOrderItemId).Select(item => new { item.SalesOrderItemId, item.Quantity })
        }, JsonOptions))));

    private static Guid MovementRequestId(Guid requestId, int orderId, int itemId) => new(SHA256.HashData(
        Encoding.UTF8.GetBytes($"sales-fulfillment:{requestId:N}:{orderId.ToString(CultureInfo.InvariantCulture)}:{itemId.ToString(CultureInfo.InvariantCulture)}"))[..16]);

    private static SalesFulfillmentResult Replay(SalesOrderFulfillment fulfillment, int userId, int orderId, string fingerprint)
    {
        if (fulfillment.CreatedBy != userId || fulfillment.SalesOrderId != orderId || fulfillment.PayloadFingerprint != fingerprint)
            throw new AppConflictException("The fulfillment request ID was already used with a different user, order or payload.");
        var original = fulfillment.ResponseSnapshot is null ? null : JsonSerializer.Deserialize<SalesFulfillmentResult>(fulfillment.ResponseSnapshot, JsonOptions);
        return original is null ? throw new AppConflictException("The fulfillment result is not available yet. Retry with the same request ID.")
            : original with { Replayed = true };
    }
}
