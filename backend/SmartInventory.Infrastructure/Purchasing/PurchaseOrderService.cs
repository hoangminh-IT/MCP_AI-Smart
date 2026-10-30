using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;
using SmartInventory.Application.Entities;
using SmartInventory.Application.Purchasing;
using SmartInventory.Application.Notifications;
using SmartInventory.Application.Stock;
using SmartInventory.Infrastructure.Data;
using SmartInventory.Infrastructure.Warehouses;

namespace SmartInventory.Infrastructure.Purchasing;

public sealed class PurchaseOrderService(AppDbContext database, IStockTransactionService stock,
    INotificationService notifications) : IPurchaseOrderService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] Statuses = [PurchaseStatuses.Draft, PurchaseStatuses.Submitted, PurchaseStatuses.Approved,
        PurchaseStatuses.PartiallyReceived, PurchaseStatuses.Received, PurchaseStatuses.Cancelled];

    public async Task<PagedResult<PurchaseOrderSummaryDto>> ListAsync(PurchaseOrderQuery query, CancellationToken cancellationToken)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue ||
            query.SupplierId is <= 0 || query.Search?.Length > 200)
            throw new AppValidationException("Use positive pagination, pageSize up to 100, a positive supplierId and search up to 200 characters.");
        await WarehouseAccess.ReadAsync(database, query.WarehouseId, cancellationToken);
        var orders = from order in database.PurchaseOrders.AsNoTracking()
            join supplier in database.Suppliers.AsNoTracking() on order.SupplierId equals supplier.Id
            join warehouse in database.Warehouses.AsNoTracking() on order.WarehouseId equals warehouse.Id
            select new { Order = order, SupplierName = supplier.Name, WarehouseCode = warehouse.Code, WarehouseName = warehouse.Name };
        if (query.WarehouseId.HasValue) orders = orders.Where(row => row.Order.WarehouseId == query.WarehouseId.Value);
        if (query.SupplierId.HasValue) orders = orders.Where(row => row.Order.SupplierId == query.SupplierId.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            orders = orders.Where(row => EF.Functions.ILike(row.Order.Code, pattern) || EF.Functions.ILike(row.SupplierName, pattern));
        }
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = Statuses.SingleOrDefault(value => value.Equals(query.Status.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new AppValidationException("Unknown purchase order status.");
            orders = orders.Where(row => row.Order.Status == status);
        }
        var sorted = (query.SortBy?.Trim().ToLowerInvariant() ?? "createdat") switch
        {
            "createdat" => query.Descending ? orders.OrderByDescending(row => row.Order.CreatedAt) : orders.OrderBy(row => row.Order.CreatedAt),
            "updatedat" => query.Descending ? orders.OrderByDescending(row => row.Order.UpdatedAt) : orders.OrderBy(row => row.Order.UpdatedAt),
            "code" => query.Descending ? orders.OrderByDescending(row => row.Order.Code) : orders.OrderBy(row => row.Order.Code),
            "suppliername" => query.Descending ? orders.OrderByDescending(row => row.SupplierName) : orders.OrderBy(row => row.SupplierName),
            "status" => query.Descending ? orders.OrderByDescending(row => row.Order.Status) : orders.OrderBy(row => row.Order.Status),
            "totalamount" => query.Descending ? orders.OrderByDescending(row => row.Order.TotalAmount) : orders.OrderBy(row => row.Order.TotalAmount),
            _ => throw new AppValidationException("Unsupported purchase order sortBy.")
        };
        var count = await orders.CountAsync(cancellationToken);
        var items = await sorted.ThenBy(row => row.Order.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(row => new PurchaseOrderSummaryDto(row.Order.Id, row.Order.Code, row.Order.SupplierId, row.SupplierName,
                row.Order.Status, row.Order.TotalAmount, row.Order.CreatedAt, row.Order.UpdatedAt,
                database.PurchaseOrderItems.Where(item => item.PurchaseOrderId == row.Order.Id).Sum(item => (long?)item.Quantity) ?? 0,
                database.PurchaseOrderItems.Where(item => item.PurchaseOrderId == row.Order.Id).Sum(item => (long?)item.ReceivedQuantity) ?? 0, row.Order.WarehouseId, row.WarehouseCode, row.WarehouseName))
            .ToListAsync(cancellationToken);
        return new(items, count, query.Page, query.PageSize);
    }

    public async Task<PurchaseOrderDetailDto> GetAsync(int id, CancellationToken cancellationToken)
    {
        var header = await (from order in database.PurchaseOrders.AsNoTracking()
            join supplier in database.Suppliers.AsNoTracking() on order.SupplierId equals supplier.Id
            join warehouse in database.Warehouses.AsNoTracking() on order.WarehouseId equals warehouse.Id
            where order.Id == id
            select new { Order = order, SupplierName = supplier.Name, WarehouseCode = warehouse.Code, WarehouseName = warehouse.Name }).SingleOrDefaultAsync(cancellationToken)
            ?? throw new AppNotFoundException("Purchase order was not found.");
        var items = await (from item in database.PurchaseOrderItems.AsNoTracking()
            join product in database.Products.AsNoTracking() on item.ProductId equals product.Id
            where item.PurchaseOrderId == id orderby item.Id
            select new PurchaseItemDto(item.Id, product.Id, product.Sku, product.Name, item.Quantity,
                item.UnitCost, item.UnitCost * item.Quantity, item.ReceivedQuantity)).ToListAsync(cancellationToken);
        var receipts = await database.PurchaseOrderReceipts.AsNoTracking().Where(receipt => receipt.PurchaseOrderId == id)
            .OrderBy(receipt => receipt.CreatedAt).ThenBy(receipt => receipt.Id).ToListAsync(cancellationToken);
        var receiptLines = await (from line in database.PurchaseOrderReceiptItems.AsNoTracking()
            join receipt in database.PurchaseOrderReceipts.AsNoTracking() on line.ReceiptId equals receipt.Id
            where receipt.PurchaseOrderId == id orderby line.Id
            select new { line.ReceiptId, Line = new PurchaseReceiptLineDto(line.PurchaseOrderItemId, line.ProductId, line.Quantity, line.MovementId) })
            .ToListAsync(cancellationToken);
        var linesByReceipt = receiptLines.ToLookup(line => line.ReceiptId, line => line.Line);
        return new(header.Order.Id, header.Order.Code, header.Order.SupplierId, header.SupplierName,
            header.Order.Status, header.Order.TotalAmount, header.Order.CreatedAt, header.Order.UpdatedAt,
            items.Sum(item => (long)item.Quantity), items.Sum(item => (long)item.ReceivedQuantity), header.Order.Notes,
            header.Order.CreatedBy, items, receipts.Select(receipt => new PurchaseReceiptDto(receipt.Id,
                receipt.RequestId, receipt.CreatedAt, receipt.CreatedBy, linesByReceipt[receipt.Id].ToList())).ToList(),
            header.Order.WarehouseId, header.WarehouseCode, header.WarehouseName);
    }

    public async Task<PurchaseOrderDetailDto> CreateAsync(int userId, PurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        var total = PurchaseRules.ValidateOrder(request);
        await ValidateActorAsync(userId, false, cancellationToken);
        await ValidateActiveReferencesAsync(request.SupplierId, request.Items.Select(item => item.ProductId).ToArray(), cancellationToken);
        await using var transaction = database.Database.CurrentTransaction is null
            ? await database.Database.BeginTransactionAsync(cancellationToken) : null;
        var warehouseId = request.WarehouseId ?? WarehouseAccess.MainId;
        await WarehouseAccess.LockActiveAsync(database, [warehouseId], cancellationToken);
        var order = new PurchaseOrder
        {
            WarehouseId = warehouseId, Code = "PO-" + Guid.NewGuid().ToString("N")[..16].ToUpperInvariant(), SupplierId = request.SupplierId,
            Notes = request.Notes, TotalAmount = total, CreatedBy = userId
        };
        database.PurchaseOrders.Add(order);
        await database.SaveChangesAsync(cancellationToken);
        database.PurchaseOrderItems.AddRange(request.Items.Select(item => new PurchaseOrderItem
        {
            PurchaseOrderId = order.Id, ProductId = item.ProductId, Quantity = item.Quantity, UnitCost = item.UnitCost
        }));
        await database.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return await GetAsync(order.Id, cancellationToken);
    }

    public async Task<PurchaseOrderDetailDto> UpdateAsync(int userId, int id, PurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        var total = PurchaseRules.ValidateOrder(request);
        await ValidateActorAsync(userId, false, cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var order = await LockOrderAsync(id, cancellationToken);
        if (order.Status != PurchaseStatuses.Draft) throw new AppConflictException("Only draft purchase orders can be edited.");
        var warehouseId = request.WarehouseId ?? order.WarehouseId;
        await WarehouseAccess.LockActiveAsync(database, [warehouseId], cancellationToken);
        order.WarehouseId = warehouseId;
        await ValidateActiveReferencesAsync(request.SupplierId, request.Items.Select(item => item.ProductId).ToArray(), cancellationToken);
        await database.PurchaseOrderItems.Where(item => item.PurchaseOrderId == id).ExecuteDeleteAsync(cancellationToken);
        database.PurchaseOrderItems.AddRange(request.Items.Select(item => new PurchaseOrderItem
        {
            PurchaseOrderId = id, ProductId = item.ProductId, Quantity = item.Quantity, UnitCost = item.UnitCost
        }));
        order.SupplierId = request.SupplierId;
        order.Notes = request.Notes;
        order.TotalAmount = total;
        order.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public Task<PurchaseOrderDetailDto> SubmitAsync(int userId, int id, CancellationToken cancellationToken) =>
        TransitionAsync(userId, id, PurchaseStatuses.Submitted, cancellationToken);
    public Task<PurchaseOrderDetailDto> ApproveAsync(int userId, int id, CancellationToken cancellationToken) =>
        TransitionAsync(userId, id, PurchaseStatuses.Approved, cancellationToken);
    public Task<PurchaseOrderDetailDto> CancelAsync(int userId, int id, CancellationToken cancellationToken) =>
        TransitionAsync(userId, id, PurchaseStatuses.Cancelled, cancellationToken);

    private async Task<PurchaseOrderDetailDto> TransitionAsync(int userId, int id, string target, CancellationToken cancellationToken)
    {
        await ValidateActorAsync(userId, target == PurchaseStatuses.Approved, cancellationToken);
        await using var transaction = database.Database.CurrentTransaction is null
            ? await database.Database.BeginTransactionAsync(cancellationToken) : null;
        var order = await LockOrderAsync(id, cancellationToken);
        if (order.Status != target)
        {
            if (target == PurchaseStatuses.Cancelled)
            {
                if (order.Status is PurchaseStatuses.PartiallyReceived or PurchaseStatuses.Received ||
                    await database.PurchaseOrderReceipts.AnyAsync(receipt => receipt.PurchaseOrderId == id, cancellationToken))
                    throw new AppConflictException("A purchase order with received goods cannot be cancelled.");
            }
            else
            {
                var expected = target == PurchaseStatuses.Submitted ? PurchaseStatuses.Draft : PurchaseStatuses.Submitted;
                if (order.Status != expected) throw new AppConflictException($"The order must be {expected} before this transition.");
                var ids = await database.PurchaseOrderItems.Where(item => item.PurchaseOrderId == id)
                    .Select(item => item.ProductId).ToArrayAsync(cancellationToken);
                await WarehouseAccess.LockActiveAsync(database, [order.WarehouseId], cancellationToken);
                await ValidateActiveReferencesAsync(order.SupplierId, ids, cancellationToken);
            }
            order.Status = target;
            order.UpdatedAt = DateTime.UtcNow;
            await database.SaveChangesAsync(cancellationToken);
            await notifications.PublishAsync($"purchase-order:{id}:{target}", "PurchaseOrder",
                target == PurchaseStatuses.Approved ? "Đơn mua hàng cần nhận" : "Cập nhật đơn mua hàng",
                $"Đơn {order.Code} đã chuyển sang trạng thái {target}.", "PurchaseOrder",
                id.ToString(CultureInfo.InvariantCulture), cancellationToken);
        }
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<PurchaseReceiptResult> ReceiveAsync(int userId, int id, PurchaseReceiptRequest request, CancellationToken cancellationToken)
    {
        PurchaseRules.ValidateReceipt(request);
        await ValidateActorAsync(userId, false, cancellationToken);
        var fingerprint = Fingerprint(userId, id, request);
        var existing = await FindReceiptAsync(request.RequestId, cancellationToken);
        if (existing is not null) return Replay(existing, userId, id, fingerprint);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var order = await LockOrderAsync(id, cancellationToken);
            existing = await FindReceiptAsync(request.RequestId, cancellationToken);
            if (existing is not null)
            {
                var replay = Replay(existing, userId, id, fingerprint);
                await transaction.CommitAsync(cancellationToken);
                return replay;
            }
            if (order.Status is not (PurchaseStatuses.Approved or PurchaseStatuses.PartiallyReceived))
                throw new AppConflictException("Only approved or partially received purchase orders can receive goods.");
            var orderItems = await database.PurchaseOrderItems.Where(item => item.PurchaseOrderId == id).ToListAsync(cancellationToken);
            var byId = orderItems.ToDictionary(item => item.Id);
            foreach (var line in request.Items)
            {
                if (!byId.TryGetValue(line.PurchaseOrderItemId, out var item))
                    throw new AppValidationException("A receipt line does not belong to this purchase order.");
                if (line.Quantity > item.Quantity - item.ReceivedQuantity)
                    throw new AppConflictException("A receipt quantity exceeds the order line's remaining quantity.");
            }
            var supplier = await database.Suppliers.FromSqlInterpolated(
                $"SELECT * FROM suppliers WHERE id = {order.SupplierId} FOR SHARE").SingleOrDefaultAsync(cancellationToken);
            if (supplier is null || !supplier.IsActive) throw new AppConflictException("The supplier must be active before receiving goods.");
            await WarehouseAccess.LockActiveAsync(database, [order.WarehouseId], cancellationToken);
            var productIds = request.Items.Select(line => byId[line.PurchaseOrderItemId].ProductId).Order().ToArray();
            var products = await database.Products.FromSqlInterpolated(
                $"SELECT * FROM products WHERE id = ANY ({productIds}) ORDER BY id FOR UPDATE").ToListAsync(cancellationToken);
            var inventory = await database.Inventories.FromSqlInterpolated(
                $"SELECT * FROM inventory WHERE product_id = ANY ({productIds}) ORDER BY product_id FOR UPDATE").ToListAsync(cancellationToken);
            var stocks = inventory.ToDictionary(item => item.ProductId);
            if (products.Count != productIds.Length || products.Any(product => !product.IsActive) || inventory.Count != productIds.Length)
                throw new AppConflictException("Every received product must be active and have an inventory record.");
            var localStocks = new Dictionary<int, WarehouseInventory>();
            foreach (var productId in productIds)
                localStocks[productId] = await WarehouseAccess.LockBalanceAsync(database, productId, order.WarehouseId, cancellationToken);
            foreach (var line in request.Items)
            {
                var quantity = stocks[byId[line.PurchaseOrderItemId].ProductId].Quantity;
                if (quantity < 0 || (long)quantity + line.Quantity > int.MaxValue ||
                    (long)localStocks[byId[line.PurchaseOrderItemId].ProductId].Quantity + line.Quantity > int.MaxValue)
                    throw new AppConflictException("A received quantity exceeds the supported inventory range.");
            }

            var receipt = new PurchaseOrderReceipt
            {
                PurchaseOrderId = id, RequestId = request.RequestId, CreatedBy = userId, PayloadFingerprint = fingerprint,
                Reason = request.Reason ?? $"Purchase order {order.Code} receipt"
            };
            database.PurchaseOrderReceipts.Add(receipt);
            await database.SaveChangesAsync(cancellationToken);
            await notifications.PublishAsync($"purchase-receipt:{receipt.Id:N}", "PurchaseOrder",
                "Đã nhận hàng mua", $"Đơn {order.Code} đã ghi nhận một lần nhận hàng.", "PurchaseOrder",
                id.ToString(CultureInfo.InvariantCulture), cancellationToken);
            var movements = new List<StockOperationDto>();
            foreach (var line in request.Items.OrderBy(line => byId[line.PurchaseOrderItemId].ProductId))
            {
                var item = byId[line.PurchaseOrderItemId];
                var movement = await stock.ReceiveInTransactionAsync(userId, new StockRequest
                {
                    WarehouseId = order.WarehouseId, ProductId = item.ProductId, Quantity = line.Quantity, Reason = receipt.Reason,
                    RequestId = MovementRequestId(request.RequestId, item.Id)
                }, "PurchaseOrder", id.ToString(CultureInfo.InvariantCulture), cancellationToken);
                movements.Add(movement);
                database.PurchaseOrderReceiptItems.Add(new PurchaseOrderReceiptItem
                {
                    ReceiptId = receipt.Id, PurchaseOrderItemId = item.Id, ProductId = item.ProductId,
                    Quantity = line.Quantity, MovementId = movement.MovementId
                });
                item.ReceivedQuantity += line.Quantity;
            }
            order.Status = orderItems.All(item => item.ReceivedQuantity == item.Quantity)
                ? PurchaseStatuses.Received : PurchaseStatuses.PartiallyReceived;
            order.UpdatedAt = DateTime.UtcNow;
            await database.SaveChangesAsync(cancellationToken);
            var response = new PurchaseReceiptResult(receipt.Id, receipt.RequestId, false, await GetAsync(id, cancellationToken), movements);
            receipt.ResponseSnapshot = JsonSerializer.Serialize(response, JsonOptions);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return response;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_purchase_order_receipts_request_id" })
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            existing = await FindReceiptAsync(request.RequestId, cancellationToken);
            if (existing is null) throw new AppConflictException("The receipt request is being processed. Retry with the same request ID.");
            return Replay(existing, userId, id, fingerprint);
        }
    }

    private Task<PurchaseOrder?> LoadOrderAsync(int id, CancellationToken cancellationToken) => database.PurchaseOrders
        .FromSqlInterpolated($"SELECT * FROM purchase_orders WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
    private async Task<PurchaseOrder> LockOrderAsync(int id, CancellationToken cancellationToken) =>
        await LoadOrderAsync(id, cancellationToken) ?? throw new AppNotFoundException("Purchase order was not found.");
    private Task<PurchaseOrderReceipt?> FindReceiptAsync(Guid requestId, CancellationToken cancellationToken) =>
        database.PurchaseOrderReceipts.AsNoTracking().SingleOrDefaultAsync(receipt => receipt.RequestId == requestId, cancellationToken);

    private async Task ValidateActorAsync(int userId, bool requireAdmin, CancellationToken cancellationToken)
    {
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (user is null || !user.IsActive || !UserRoles.IsValid(user.Role)) throw new AppUnauthorizedException("The account is no longer available.");
        if (!UserRoles.CanManageInventory(user.Role)) throw new AppForbiddenException("An inventory operator account is required.");
        if (requireAdmin && user.Role != UserRoles.Admin) throw new AppForbiddenException("Only administrators may approve purchase orders.");
    }

    private async Task ValidateActiveReferencesAsync(int supplierId, int[] productIds, CancellationToken cancellationToken)
    {
        if (!await database.Suppliers.AnyAsync(supplier => supplier.Id == supplierId && supplier.IsActive, cancellationToken))
            throw new AppConflictException("Select an active supplier.");
        if (await database.Products.CountAsync(product => productIds.Contains(product.Id) && product.IsActive, cancellationToken) != productIds.Length)
            throw new AppConflictException("Every order product must be active.");
    }

    private static string Fingerprint(int userId, int orderId, PurchaseReceiptRequest request) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            userId, orderId, reason = request.Reason ?? string.Empty,
            items = request.Items.OrderBy(item => item.PurchaseOrderItemId).Select(item => new { item.PurchaseOrderItemId, item.Quantity })
        }, JsonOptions))));

    private static Guid MovementRequestId(Guid receiptRequestId, int itemId) => new(SHA256.HashData(
        Encoding.UTF8.GetBytes($"purchase-receipt:{receiptRequestId:N}:{itemId.ToString(CultureInfo.InvariantCulture)}"))[..16]);

    private static PurchaseReceiptResult Replay(PurchaseOrderReceipt receipt, int userId, int orderId, string fingerprint)
    {
        if (receipt.CreatedBy != userId || receipt.PurchaseOrderId != orderId || receipt.PayloadFingerprint != fingerprint)
            throw new AppConflictException("The receipt request ID was already used with a different user, order or payload.");
        var original = receipt.ResponseSnapshot is null ? null : JsonSerializer.Deserialize<PurchaseReceiptResult>(receipt.ResponseSnapshot, JsonOptions);
        return original is null ? throw new AppConflictException("The receipt result is not available yet. Retry with the same request ID.")
            : original with { Replayed = true };
    }
}
