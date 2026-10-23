using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;
using SmartInventory.Application.Entities;
using SmartInventory.Application.Mcp;
using SmartInventory.Application.Stock;
using SmartInventory.Infrastructure.Catalog;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Stock;

/// <summary>All stock mutations lock product then inventory and commit the quantity and movement together.</summary>
public sealed class StockService(AppDbContext database, CatalogService catalog, IInventoryReadService summaries) : IStockService
{
    public Task<StockOperationDto> ReceiveAsync(int userId, StockRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(userId, StockMovementTypes.Receipt, request, cancellationToken);

    public Task<StockOperationDto> IssueAsync(int userId, StockRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(userId, StockMovementTypes.Issue, request, cancellationToken);

    public Task<StockOperationDto> AdjustAsync(int userId, StockRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(userId, StockMovementTypes.Adjustment, request, cancellationToken);

    public async Task<PagedResult<ProductDto>> GetInventoryAsync(InventoryQuery query, CancellationToken cancellationToken)
    {
        var catalogQuery = new CatalogQuery
        {
            Search = query.Search, CategoryId = query.CategoryId, Page = query.Page, PageSize = query.PageSize,
            SortBy = query.SortBy, Descending = query.Descending, IncludeInactive = query.IncludeInactive
        };
        CatalogService.ValidateQuery(catalogQuery);
        var products = catalog.FilterProducts(catalogQuery);
        products = (query.StockState?.Trim().ToLowerInvariant() ?? "all") switch
        {
            "all" => products,
            "low" => products.Where(product => product.Quantity < product.MinStock),
            "out" => products.Where(product => product.Quantity == 0),
            _ => throw new AppValidationException("stockState must be all, low or out.")
        };
        var count = await products.CountAsync(cancellationToken);
        var items = await CatalogService.SortProducts(products, catalogQuery)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return new(items, count, query.Page, query.PageSize);
    }

    public async Task<ProductDto> GetInventoryProductAsync(int productId, CancellationToken cancellationToken)
    {
        if (productId <= 0) throw new AppValidationException("productId must be positive.");
        var product = await catalog.GetProductAsync(productId, cancellationToken);
        return product.IsActive ? product : throw new AppNotFoundException("Active inventory product was not found.");
    }

    public Task<InventorySummaryDto> GetSummaryAsync(CancellationToken cancellationToken) =>
        summaries.GetInventorySummaryAsync(cancellationToken);

    public async Task<PagedResult<StockMovementDetailDto>> GetMovementsAsync(StockMovementQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue)
            throw new AppValidationException("page must be positive and pageSize must be between 1 and 100.");
        if (query.ProductId is <= 0) throw new AppValidationException("productId must be positive when supplied.");
        var from = ParseUtc(query.From, "from");
        var to = ParseUtc(query.To, "to");
        if (from.HasValue && to.HasValue && from > to)
            throw new AppValidationException("from must be before or equal to to.");
        var movements = database.StockMovements.AsNoTracking().AsQueryable();
        if (query.ProductId.HasValue) movements = movements.Where(movement => movement.ProductId == query.ProductId.Value);
        if (from.HasValue) movements = movements.Where(movement => movement.CreatedAt >= from.Value);
        if (to.HasValue) movements = movements.Where(movement => movement.CreatedAt <= to.Value);
        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            var type = query.Type.Trim().ToLowerInvariant();
            if (type.Length > 32) throw new AppValidationException("type must be at most 32 characters.");
            movements = movements.Where(movement => movement.Type.ToLower() == type);
        }
        var count = await movements.CountAsync(cancellationToken);
        var items = await (from movement in movements
            join product in database.Products.AsNoTracking() on movement.ProductId equals product.Id
            orderby movement.CreatedAt descending, movement.Id descending
            select new StockMovementDetailDto(movement.Id, product.Id, product.Sku, product.Name,
                movement.Type, movement.Quantity, movement.QuantityBefore, movement.QuantityAfter,
                movement.Reason, movement.CreatedBy, movement.CreatedAt, movement.RequestId,
                movement.ReferenceType, movement.ReferenceId))
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return new(items, count, query.Page, query.PageSize);
    }

    private async Task<StockOperationDto> ExecuteAsync(int userId, string type, StockRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(type, request);
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null || !user.IsActive || !UserRoles.IsValid(user.Role))
            throw new AppUnauthorizedException("The account is no longer available.");
        if (type == StockMovementTypes.Adjustment && user.Role != UserRoles.Admin)
            throw new AppForbiddenException("Only administrators may adjust inventory.");

        var replay = await FindReplayAsync(request.RequestId, cancellationToken);
        if (replay is not null) return ValidateReplay(replay, userId, type, request);

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Interpolated values become Npgsql parameters; lock ordering is identical for every mutation.
            var product = await database.Products.FromSqlInterpolated(
                    $"SELECT * FROM products WHERE id = {request.ProductId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new AppNotFoundException("Product was not found.");
            var inventory = await database.Inventories.FromSqlInterpolated(
                    $"SELECT * FROM inventory WHERE product_id = {request.ProductId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new AppConflictException("The product has no inventory record. Resolve its inventory setup before posting stock.");

            // The first replay read may precede a concurrent transaction's commit, so check again after the lock.
            replay = await FindReplayAsync(request.RequestId, cancellationToken);
            if (replay is not null)
            {
                var response = ValidateReplay(replay, userId, type, request);
                await transaction.CommitAsync(cancellationToken);
                return response;
            }
            if (!product.IsActive) throw new AppConflictException("Inactive products cannot receive stock transactions.");
            if (inventory.Quantity < 0) throw new AppConflictException("Current inventory quantity is invalid.");

            var before = inventory.Quantity;
            var after = type switch
            {
                StockMovementTypes.Receipt => checked((long)before + request.Quantity),
                StockMovementTypes.Issue => (long)before - request.Quantity,
                _ => request.Quantity
            };
            if (after < 0) throw new AppConflictException("Insufficient stock. The requested issue exceeds the current quantity.");
            if (after > int.MaxValue) throw new AppValidationException("The resulting stock quantity exceeds the supported maximum.");

            var now = DateTime.UtcNow;
            inventory.Quantity = (int)after;
            inventory.UpdatedAt = now;
            var movement = new StockMovement
            {
                ProductId = product.Id, Type = type,
                Quantity = type == StockMovementTypes.Adjustment ? Math.Abs((int)after - before) : request.Quantity,
                QuantityBefore = before, QuantityAfter = (int)after, Reason = request.Reason,
                CreatedBy = userId, CreatedAt = now, RequestId = request.RequestId
            };
            database.StockMovements.Add(movement);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToOperation(movement, false);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_stock_movements_request_id" })
        {
            // A global request ID may race on a different product, whose row lock is independent.
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            replay = await FindReplayAsync(request.RequestId, cancellationToken);
            if (replay is null) throw new AppConflictException("The request ID was used concurrently. Reload and retry with the same request.");
            return ValidateReplay(replay, userId, type, request);
        }
    }

    private Task<StockMovement?> FindReplayAsync(Guid requestId, CancellationToken cancellationToken) =>
        database.StockMovements.AsNoTracking().SingleOrDefaultAsync(movement => movement.RequestId == requestId, cancellationToken);

    private static StockOperationDto ValidateReplay(StockMovement movement, int userId, string type, StockRequest request)
    {
        var quantityMatches = type == StockMovementTypes.Adjustment
            ? movement.QuantityAfter == request.Quantity : movement.Quantity == request.Quantity;
        if (movement.CreatedBy != userId || movement.ProductId != request.ProductId || movement.Type != type ||
            movement.Reason != request.Reason || !quantityMatches)
            throw new AppConflictException("The request ID has already been used with a different user or payload.");
        return ToOperation(movement, true);
    }

    private static StockOperationDto ToOperation(StockMovement movement, bool replayed) =>
        new(movement.Id, movement.ProductId, movement.Type, movement.Quantity,
            movement.QuantityBefore ?? throw new AppConflictException("This historical movement cannot be replayed."),
            movement.QuantityAfter ?? throw new AppConflictException("This historical movement cannot be replayed."),
            movement.Reason, movement.CreatedBy ?? throw new AppConflictException("This historical movement cannot be replayed."),
            movement.CreatedAt, movement.RequestId ?? throw new AppConflictException("This historical movement cannot be replayed."), replayed);

    private static void ValidateRequest(string type, StockRequest request)
    {
        request.Reason = request.Reason?.Trim() ?? string.Empty;
        if (request.ProductId <= 0 || request.RequestId == Guid.Empty || request.Reason.Length is < 1 or > 1000 ||
            request.Quantity < 0 || type != StockMovementTypes.Adjustment && request.Quantity == 0)
            throw new AppValidationException("Use a positive productId, a nonempty requestId, a reason of 1 to 1,000 characters, and a positive receipt/issue quantity or nonnegative adjustment target.");
    }

    private static DateTime? ParseUtc(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (!Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|\+00:00)$", RegexOptions.CultureInvariant) ||
            !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) || parsed.Offset != TimeSpan.Zero)
            throw new AppValidationException($"{field} must be an ISO 8601 UTC timestamp ending in Z or +00:00.");
        return parsed.UtcDateTime;
    }
}
