using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;
using SmartInventory.Application.Mcp;
using SmartInventory.Infrastructure.Catalog;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Mcp;

public sealed class InventoryReadService(AppDbContext database) : IInventoryReadService
{
    private IQueryable<ProductDto> ActiveProducts => CatalogQueryFactory.Products(database).Where(product => product.IsActive);

    public async Task<ProductDto> GetProductBySkuAsync(string sku, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sku) || sku.Trim().Length > 80)
            throw new AppValidationException("sku must contain between 1 and 80 characters.");
        var normalized = sku.Trim().ToUpperInvariant();
        var products = await ActiveProducts.Where(product => product.Sku.ToUpper() == normalized)
            .OrderBy(product => product.Id).Take(2).ToListAsync(cancellationToken);
        if (products.Count > 1)
            throw new AppConflictException("More than one active product matches this SKU. Resolve the duplicate SKUs.");
        return products.FirstOrDefault() ?? throw new AppNotFoundException("Active product was not found.");
    }

    public async Task<PagedResult<ProductDto>> GetLowStockProductsAsync(int? threshold, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        if (threshold is < 0)
            throw new AppValidationException("threshold must be zero or greater, or omitted to use each product's minimum stock.");
        var products = threshold.HasValue ? ActiveProducts.Where(product => product.Quantity < threshold.Value)
            : ActiveProducts.Where(product => product.Quantity < product.MinStock);
        return await PageProductsAsync(products, page, pageSize, cancellationToken);
    }

    public Task<PagedResult<ProductDto>> GetOutOfStockProductsAsync(int page, int pageSize, CancellationToken cancellationToken) =>
        PageProductsAsync(ActiveProducts.Where(product => product.Quantity == 0), page, pageSize, cancellationToken);

    public async Task<InventorySummaryDto> GetInventorySummaryAsync(CancellationToken cancellationToken)
    {
        var summary = await ActiveProducts.GroupBy(_ => 1).Select(group => new
        {
            TotalProducts = group.Count(), TotalQuantity = group.Sum(product => (long)product.Quantity),
            CostValue = group.Sum(product => product.CostPrice * product.Quantity),
            RetailValue = group.Sum(product => product.Price * product.Quantity),
            LowStock = group.Count(product => product.Quantity < product.MinStock),
            OutOfStock = group.Count(product => product.Quantity == 0)
        }).SingleOrDefaultAsync(cancellationToken);
        var categories = await database.Categories.CountAsync(cancellationToken);
        var movements = await database.StockMovements.LongCountAsync(cancellationToken);
        return new(summary?.TotalProducts ?? 0, categories, summary?.TotalQuantity ?? 0,
            summary?.CostValue ?? 0, summary?.RetailValue ?? 0, summary?.LowStock ?? 0,
            summary?.OutOfStock ?? 0, movements, DateTime.UtcNow);
    }

    public async Task<PagedResult<StockMovementDto>> GetStockMovementHistoryAsync(int? productId, int page,
        int pageSize, CancellationToken cancellationToken)
    {
        ValidatePage(page, pageSize);
        if (productId is <= 0) throw new AppValidationException("productId must be positive when supplied.");
        var query = database.StockMovements.AsNoTracking().AsQueryable();
        if (productId.HasValue) query = query.Where(movement => movement.ProductId == productId.Value);
        var count = await query.CountAsync(cancellationToken);
        var items = await (from movement in query
            join product in database.Products.AsNoTracking() on movement.ProductId equals product.Id
            orderby movement.CreatedAt descending, movement.Id descending
            select new StockMovementDto(movement.Id, product.Id, product.Sku, product.Name,
                movement.Type, movement.Quantity, movement.CreatedAt, movement.QuantityBefore, movement.QuantityAfter,
                movement.Reason, movement.CreatedBy, movement.RequestId, movement.ReferenceType, movement.ReferenceId))
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new(items, count, page, pageSize);
    }

    public async Task<IReadOnlyList<CategoryInventoryDto>> GetCategorySummaryAsync(CancellationToken cancellationToken)
    {
        var products = ActiveProducts;
        return await database.Categories.AsNoTracking().OrderBy(category => category.Name).ThenBy(category => category.Id)
            .Select(category => new CategoryInventoryDto(category.Id, category.Name,
                products.Count(product => product.CategoryId == category.Id),
                products.Where(product => product.CategoryId == category.Id).Sum(product => (long?)product.Quantity) ?? 0,
                products.Where(product => product.CategoryId == category.Id).Sum(product => (decimal?)(product.CostPrice * product.Quantity)) ?? 0,
                products.Where(product => product.CategoryId == category.Id).Sum(product => (decimal?)(product.Price * product.Quantity)) ?? 0))
            .ToListAsync(cancellationToken);
    }

    private static async Task<PagedResult<ProductDto>> PageProductsAsync(IQueryable<ProductDto> products,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        ValidatePage(page, pageSize);
        var count = await products.CountAsync(cancellationToken);
        var items = await products.OrderBy(product => product.Quantity).ThenBy(product => product.Name).ThenBy(product => product.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new(items, count, page, pageSize);
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new AppValidationException("page must be positive and pageSize must be between 1 and 100.");
    }
}
