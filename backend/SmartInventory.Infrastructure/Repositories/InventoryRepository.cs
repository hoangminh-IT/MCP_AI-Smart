using System.Data;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.DTOs.Inventory;
using SmartInventory.Application.Entities;
using SmartInventory.Application.Interfaces;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Repositories;

/// <summary>PostgreSQL persistence for inventory queries and atomic stock movements.</summary>
public sealed class InventoryRepository(AppDbContext dbContext) : IInventoryRepository
{
    private sealed record InventoryRow(Product Product, int Quantity);
    public async Task<InventoryItemResponse?> GetByProductIdAsync(int productId, CancellationToken cancellationToken = default) =>
        await InventoryRows().Where(row => row.Product.Id == productId)
            .Select(row => ToInventoryItem(row.Product, row.Quantity))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<InventoryItemResponse?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default) =>
        await InventoryRows().Where(row => row.Product.Sku == sku)
            .Select(row => ToInventoryItem(row.Product, row.Quantity))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResponse<InventoryItemResponse>> GetInventoryAsync(InventoryQueryRequest request, CancellationToken cancellationToken = default)
    {
        var rows = InventoryRows();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search.Trim()}%";
            rows = rows.Where(row => EF.Functions.ILike(row.Product.Sku, pattern) || EF.Functions.ILike(row.Product.Name, pattern));
        }
        if (request.LowStockOnly == true)
            rows = rows.Where(row => row.Quantity <= row.Product.MinStock);

        var total = await rows.LongCountAsync(cancellationToken);
        var items = await rows.OrderBy(row => row.Product.Name)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(row => ToInventoryItem(row.Product, row.Quantity)).ToListAsync(cancellationToken);
        return new(items, request.Page, request.PageSize, total);
    }

    public async Task<PagedResponse<InventoryItemResponse>> GetLowStockAsync(LowStockQueryRequest request, CancellationToken cancellationToken = default)
    {
        var rows = InventoryRows().Where(row => row.Quantity <= row.Product.MinStock);
        var total = await rows.LongCountAsync(cancellationToken);
        var items = await rows.OrderBy(row => row.Quantity).ThenBy(row => row.Product.Name)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(row => ToInventoryItem(row.Product, row.Quantity)).ToListAsync(cancellationToken);
        return new(items, request.Page, request.PageSize, total);
    }

    public async Task<InventorySummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var products = dbContext.Products.AsNoTracking();
        var aggregates = await products
            .GroupJoin(dbContext.Inventories.AsNoTracking(), product => product.Id, stock => stock.ProductId,
                (product, stocks) => new { product.MinStock, Quantity = stocks.Select(stock => (int?)stock.Quantity).FirstOrDefault() ?? 0 })
            .GroupBy(_ => 1)
            .Select(group => new
            {
                ProductCount = group.Count(),
                TotalQuantity = group.Sum(row => (long)row.Quantity),
                LowStockCount = group.Count(row => row.Quantity <= row.MinStock),
                OutOfStockCount = group.Count(row => row.Quantity == 0)
            }).SingleOrDefaultAsync(cancellationToken);

        return aggregates is null
            ? new InventorySummaryResponse(0, 0, 0, 0)
            : new InventorySummaryResponse(aggregates.ProductCount, aggregates.TotalQuantity,
                aggregates.LowStockCount, aggregates.OutOfStockCount);
    }

    public async Task<PagedResponse<StockMovementResponse>> GetMovementsAsync(StockMovementQueryRequest request, CancellationToken cancellationToken = default)
    {
        var movements = dbContext.StockMovements.AsNoTracking()
            .Join(dbContext.Products.AsNoTracking(), movement => movement.ProductId, product => product.Id,
                (movement, product) => new { Movement = movement, Product = product });
        if (request.ProductId.HasValue)
            movements = movements.Where(row => row.Movement.ProductId == request.ProductId.Value);
        if (request.FromUtc.HasValue)
            movements = movements.Where(row => row.Movement.CreatedAt >= request.FromUtc.Value);
        if (request.ToUtc.HasValue)
            movements = movements.Where(row => row.Movement.CreatedAt <= request.ToUtc.Value);

        var total = await movements.LongCountAsync(cancellationToken);
        var items = await movements.OrderByDescending(row => row.Movement.CreatedAt).ThenByDescending(row => row.Movement.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(row => new StockMovementResponse(row.Movement.Id, row.Movement.ProductId,
                row.Product.Sku, row.Product.Name,
                row.Movement.Type == "Import" ? StockMovementType.Import : StockMovementType.Export,
                row.Movement.Quantity, row.Movement.Reason, row.Movement.CreatedAt))
            .ToListAsync(cancellationToken);
        return new(items, request.Page, request.PageSize, total);
    }

    public async Task<StockTransactionResponse> ApplyStockMovementAsync(
        int productId, int quantity, StockMovementType type, string reason,
        CancellationToken cancellationToken = default)
    {
        if (productId <= 0) throw new ArgumentOutOfRangeException(nameof(productId));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reason is required.", nameof(reason));

        // Serializable isolation protects the read/check/write decision when concurrent requests race.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var productExists = await dbContext.Products.AsNoTracking()
                .AnyAsync(product => product.Id == productId, cancellationToken);
            if (!productExists)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new(StockTransactionOutcome.ProductNotFound, productId, null, null, "Product was not found.");
            }

            var current = await dbContext.Inventories.AsNoTracking()
                .Where(stock => stock.ProductId == productId)
                .Select(stock => (int?)stock.Quantity)
                .SingleOrDefaultAsync(cancellationToken);
            var before = current ?? 0;
            if (type == StockMovementType.Export && before < quantity)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new(StockTransactionOutcome.InsufficientStock, productId, before, before,
                    "Insufficient stock for this export.");
            }

            var after = type == StockMovementType.Import
                ? checked(before + quantity)
                : before - quantity;

            if (current.HasValue)
            {
                var updated = type == StockMovementType.Import
                    ? await dbContext.Inventories.Where(stock => stock.ProductId == productId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(stock => stock.Quantity, stock => stock.Quantity + quantity), cancellationToken)
                    : await dbContext.Inventories.Where(stock => stock.ProductId == productId && stock.Quantity >= quantity)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(stock => stock.Quantity, stock => stock.Quantity - quantity), cancellationToken);
                if (updated != 1)
                    throw new DbUpdateConcurrencyException("Inventory changed while applying the movement; retry the operation.");
            }
            else
            {
                dbContext.Inventories.Add(new Inventory { ProductId = productId, Quantity = after });
            }

            dbContext.StockMovements.Add(new StockMovement
            {
                ProductId = productId,
                Type = type.ToString(),
                Quantity = quantity,
                Reason = reason.Trim(),
                CreatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(StockTransactionOutcome.Applied, productId, before, after);
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // Preserve the original persistence/cancellation exception.
            }
            throw;
        }
    }

    private IQueryable<InventoryRow> InventoryRows()
    {
        // Left join includes products without an inventory row as zero on-hand.
        return dbContext.Products.AsNoTracking()
            .GroupJoin(dbContext.Inventories.AsNoTracking(), product => product.Id, stock => stock.ProductId,
                (product, stocks) => new { Product = product, Stocks = stocks })
            .SelectMany(row => row.Stocks.DefaultIfEmpty(), (row, stock) => new { row.Product, Quantity = stock == null ? 0 : stock.Quantity })
            .Select(row => new InventoryRow(row.Product, row.Quantity));
    }

    private static InventoryItemResponse ToInventoryItem(Product product, int quantity) =>
        new(product.Id, product.Sku, product.Name, quantity, product.MinStock,
            quantity == 0 ? InventoryStatus.OutOfStock : quantity <= product.MinStock ? InventoryStatus.LowStock : InventoryStatus.InStock);
}
