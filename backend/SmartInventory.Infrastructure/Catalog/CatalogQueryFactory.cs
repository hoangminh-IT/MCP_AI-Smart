using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Catalog;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Catalog;

internal static class CatalogQueryFactory
{
    public static IQueryable<ProductDto> Products(AppDbContext database) =>
        from product in database.Products.AsNoTracking()
        join category in database.Categories.AsNoTracking() on product.CategoryId equals category.Id
        join stock in database.Inventories.AsNoTracking() on product.Id equals stock.ProductId into productStock
        from stock in productStock.DefaultIfEmpty()
        select new ProductDto
        {
            Id = product.Id, CategoryId = product.CategoryId, CategoryName = category.Name,
            Sku = product.Sku, Name = product.Name, Description = product.Description,
            Price = product.Price, CostPrice = product.CostPrice, MinStock = product.MinStock,
            ImageUrl = product.ImageUrl, IsActive = product.IsActive,
            Quantity = stock == null ? 0 : stock.Quantity, CreatedAt = product.CreatedAt, UpdatedAt = product.UpdatedAt
        };
}
