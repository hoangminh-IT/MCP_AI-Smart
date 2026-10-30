using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;
using SmartInventory.Application.Entities;
using SmartInventory.Application.Stock;
using SmartInventory.Infrastructure.Warehouses;

namespace SmartInventory.Infrastructure.Stock;

public sealed partial class StockService
{
    private static readonly JsonSerializerOptions TransferJson = new(JsonSerializerDefaults.Web);

    public async Task<StockTransferDto> TransferAsync(int userId, StockTransferRequest request, CancellationToken cancellationToken)
    {
        request.Reason = request.Reason?.Trim() ?? "";
        if (request.ProductId <= 0 || request.SourceWarehouseId <= 0 || request.DestinationWarehouseId <= 0 ||
            request.SourceWarehouseId == request.DestinationWarehouseId || request.Quantity <= 0 ||
            request.RequestId == Guid.Empty || request.Reason.Length is < 1 or > 1000)
            throw new AppValidationException("Use distinct positive warehouse IDs, a positive product and quantity, a request UUID and a reason of 1..1,000 characters.");
        if (!await database.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.IsActive &&
            (user.Role == UserRoles.Admin || user.Role == UserRoles.Staff), cancellationToken))
            throw new AppUnauthorizedException("An active authorized account is required.");
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        { userId, request.ProductId, request.SourceWarehouseId, request.DestinationWarehouseId, request.Quantity, request.Reason }, TransferJson)));
        var replay = await FindTransferAsync(request.RequestId, cancellationToken);
        if (replay is not null) return ReplayTransfer(replay, userId, fingerprint);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var warehouses = await WarehouseAccess.LockActiveAsync(database,
                [request.SourceWarehouseId, request.DestinationWarehouseId], cancellationToken);
            var product = await database.Products.FromSqlInterpolated($"SELECT * FROM products WHERE id = {request.ProductId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken) ?? throw new AppNotFoundException("Product was not found.");
            var aggregate = await database.Inventories.FromSqlInterpolated($"SELECT * FROM inventory WHERE product_id = {request.ProductId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken) ?? throw new AppConflictException("The product has no inventory record.");
            await database.Entry(product).ReloadAsync(cancellationToken);
            await database.Entry(aggregate).ReloadAsync(cancellationToken);
            replay = await FindTransferAsync(request.RequestId, cancellationToken);
            if (replay is not null)
            {
                var result = ReplayTransfer(replay, userId, fingerprint);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            if (!product.IsActive) throw new AppConflictException("Inactive products cannot be transferred.");
            var balances = new Dictionary<int, WarehouseInventory>();
            foreach (var id in new[] { request.SourceWarehouseId, request.DestinationWarehouseId }.Order())
                balances[id] = await WarehouseAccess.LockBalanceAsync(database, product.Id, id, cancellationToken);
            var source = balances[request.SourceWarehouseId];
            var destination = balances[request.DestinationWarehouseId];
            if (source.Quantity < request.Quantity) throw new AppConflictException("Insufficient stock in the source warehouse.");
            if ((long)destination.Quantity + request.Quantity > int.MaxValue)
                throw new AppValidationException("The destination warehouse quantity exceeds the supported maximum.");
            var now = DateTime.UtcNow;
            var transfer = new StockTransfer { RequestId = request.RequestId, ProductId = product.Id,
                SourceWarehouseId = source.WarehouseId, DestinationWarehouseId = destination.WarehouseId,
                Quantity = request.Quantity, Reason = request.Reason, CreatedBy = userId, CreatedAt = now, PayloadFingerprint = fingerprint };
            var outgoing = TransferMovement(transfer, aggregate.Quantity, source, -request.Quantity, StockMovementTypes.TransferOut, "out", now);
            var incoming = TransferMovement(transfer, aggregate.Quantity, destination, request.Quantity, StockMovementTypes.TransferIn, "in", now);
            aggregate.UpdatedAt = now;
            database.StockTransfers.Add(transfer);
            database.StockMovements.AddRange(outgoing, incoming);
            // Both local balances, both movements, metadata audit and transfer head share one transaction.
            // Aggregate quantity is unchanged, so transfers never publish temporary global stock alerts.
            await database.SaveChangesAsync(cancellationToken);
            var response = new StockTransferDto(transfer.Id, transfer.RequestId, false, transfer.ProductId,
                transfer.SourceWarehouseId, transfer.DestinationWarehouseId, transfer.Quantity, transfer.Reason,
                transfer.CreatedBy, transfer.CreatedAt,
                [ToOperation(outgoing, false, warehouses.Single(item => item.Id == outgoing.WarehouseId)),
                    ToOperation(incoming, false, warehouses.Single(item => item.Id == incoming.WarehouseId))]);
            transfer.ResponseSnapshot = JsonSerializer.Serialize(response, TransferJson);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return response;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_stock_transfers_request_id" })
        {
            try { await transaction.RollbackAsync(cancellationToken); }
            finally { database.ChangeTracker.Clear(); }
            replay = await FindTransferAsync(request.RequestId, cancellationToken);
            return replay is not null ? ReplayTransfer(replay, userId, fingerprint)
                : throw new AppConflictException("The transfer request is being processed. Retry with the same request ID.");
        }
        catch
        {
            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch
            {
                // A failed deferred COMMIT may already have completed the provider transaction.
                // Cleanup is best effort; retain the original business/commit exception below.
            }
            finally { database.ChangeTracker.Clear(); }
            throw;
        }
    }

    public async Task<PagedResult<StockTransferDto>> GetTransfersAsync(StockTransferQuery query, CancellationToken cancellationToken)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue || query.ProductId is <= 0)
            throw new AppValidationException("Use positive filters and pagination, with pageSize up to 100.");
        if (query.RequestId == Guid.Empty) throw new AppValidationException("requestId must be a nonempty UUID when supplied.");
        await WarehouseAccess.ReadAsync(database, query.WarehouseId, cancellationToken);
        var rows = database.StockTransfers.AsNoTracking().AsQueryable();
        if (query.RequestId.HasValue) rows = rows.Where(item => item.RequestId == query.RequestId.Value);
        if (query.ProductId.HasValue) rows = rows.Where(item => item.ProductId == query.ProductId);
        if (query.WarehouseId.HasValue) rows = rows.Where(item => item.SourceWarehouseId == query.WarehouseId || item.DestinationWarehouseId == query.WarehouseId);
        var count = await rows.CountAsync(cancellationToken);
        var items = await rows.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return new(items.Select(ReadTransfer).ToArray(), count, query.Page, query.PageSize);
    }
    public async Task<StockTransferDto> GetTransferAsync(Guid id, CancellationToken cancellationToken) =>
        ReadTransfer(await database.StockTransfers.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new AppNotFoundException("Stock transfer was not found."));

    private Task<StockTransfer?> FindTransferAsync(Guid requestId, CancellationToken cancellationToken) =>
        database.StockTransfers.AsNoTracking().SingleOrDefaultAsync(item => item.RequestId == requestId, cancellationToken);
    private static StockTransferDto ReadTransfer(StockTransfer transfer) =>
        transfer.ResponseSnapshot is not null ? JsonSerializer.Deserialize<StockTransferDto>(transfer.ResponseSnapshot, TransferJson)
            ?? throw new AppConflictException("Transfer history is incomplete.") : throw new AppConflictException("Transfer history is incomplete.");
    private static StockTransferDto ReplayTransfer(StockTransfer transfer, int userId, string fingerprint)
    {
        if (transfer.CreatedBy != userId || transfer.PayloadFingerprint != fingerprint)
            throw new AppConflictException("The request ID has already been used with a different user or transfer payload.");
        return ReadTransfer(transfer) with { Replayed = true };
    }
    private static StockMovement TransferMovement(StockTransfer transfer, int aggregateQuantity, WarehouseInventory balance,
        int delta, string type, string direction, DateTime now)
    {
        var movement = new StockMovement { ProductId = transfer.ProductId, WarehouseId = balance.WarehouseId,
            Type = type, Quantity = transfer.Quantity, QuantityBefore = aggregateQuantity, QuantityAfter = aggregateQuantity,
            WarehouseQuantityBefore = balance.Quantity, WarehouseQuantityAfter = balance.Quantity + delta,
            Reason = transfer.Reason, CreatedBy = transfer.CreatedBy, CreatedAt = now, ReferenceType = "StockTransfer",
            ReferenceId = transfer.Id.ToString(), RequestId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"warehouse-transfer:{transfer.RequestId:N}:{direction}"))[..16]) };
        balance.Quantity += delta;
        balance.UpdatedAt = now;
        return movement;
    }
}
