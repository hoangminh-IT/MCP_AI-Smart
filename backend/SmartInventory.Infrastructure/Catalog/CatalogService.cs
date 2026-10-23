using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;
using SmartInventory.Application.Entities;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Catalog;

public sealed class CatalogService(AppDbContext database) : IProductService, ICategoryService
{
    public async Task<PagedResult<ProductDto>> GetProductsAsync(CatalogQuery query, CancellationToken cancellationToken)
    {
        ValidateQuery(query);
        var products = SortProducts(FilterProducts(query), query);
        var total = await products.CountAsync(cancellationToken);
        var items = await products.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return new(items, total, query.Page, query.PageSize);
    }

    public async Task<ProductDto> GetProductAsync(int id, CancellationToken cancellationToken) =>
        await ProductProjection().SingleOrDefaultAsync(product => product.Id == id, cancellationToken)
        ?? throw new AppNotFoundException("Product was not found.");

    public async Task<ProductDto> CreateProductAsync(ProductRequest request, CancellationToken cancellationToken)
    {
        NormalizeAndValidate(request);
        await ValidateCategoryAsync(request.CategoryId, cancellationToken);
        await ValidateSkuAsync(request.Sku, null, cancellationToken);
        var product = new Product();
        Apply(product, request);
        database.Products.Add(product);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        database.Inventories.Add(new Inventory { ProductId = product.Id, Quantity = 0 });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetProductAsync(product.Id, cancellationToken);
    }

    public async Task<ProductDto> UpdateProductAsync(int id, ProductRequest request, CancellationToken cancellationToken)
    {
        NormalizeAndValidate(request);
        var product = await database.Products.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new AppNotFoundException("Product was not found.");
        await ValidateCategoryAsync(request.CategoryId, cancellationToken);
        await ValidateSkuAsync(request.Sku, id, cancellationToken);
        Apply(product, request);
        await database.SaveChangesAsync(cancellationToken);
        return await GetProductAsync(id, cancellationToken);
    }

    public async Task DeactivateProductAsync(int id, CancellationToken cancellationToken)
    {
        var product = await database.Products.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new AppNotFoundException("Product was not found.");
        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task SetProductImageAsync(int id, string imageUrl, CancellationToken cancellationToken)
    {
        var product = await database.Products.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new AppNotFoundException("Product was not found.");
        product.ImageUrl = imageUrl;
        product.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductDto>> ExportProductsAsync(CatalogQuery query, CancellationToken cancellationToken)
    {
        ValidateQuery(query);
        var products = SortProducts(FilterProducts(query), query);
        // Keep memory bounded and tell the user to filter large exports.
        if (await products.CountAsync(cancellationToken) > 10000)
            throw new AppValidationException("Export is limited to 10,000 products. Apply a search or category filter.");
        return await products.ToListAsync(cancellationToken);
    }

    public async Task<ProductImportResult> ImportProductsAsync(IReadOnlyList<ProductRequest> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count is < 1 or > 1000)
            throw new AppValidationException("CSV must contain between 1 and 1,000 product rows.");

        var errors = new Dictionary<string, string[]>();
        var seenSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < rows.Count; index++)
        {
            try { NormalizeAndValidate(rows[index]); }
            catch (AppValidationException exception)
            {
                errors[$"rows[{index + 1}]"] = [exception.Message];
            }
            if (!seenSkus.Add(rows[index].Sku))
                errors[$"rows[{index + 1}].sku"] = ["SKU appears more than once in the file."];
        }
        if (errors.Count > 0)
            throw new AppValidationException("CSV validation failed. No products were changed.", errors);

        var categoryIds = rows.Select(row => row.CategoryId).Distinct().ToArray();
        var validCategories = await database.Categories.AsNoTracking().Where(category => categoryIds.Contains(category.Id))
            .Select(category => category.Id).ToListAsync(cancellationToken);
        for (var index = 0; index < rows.Count; index++)
            if (!validCategories.Contains(rows[index].CategoryId))
                errors[$"rows[{index + 1}].categoryId"] = ["Category does not exist."];
        if (errors.Count > 0)
            throw new AppValidationException("CSV validation failed. No products were changed.", errors);

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var skus = rows.Select(row => row.Sku).ToArray();
        var existing = await database.Products.Where(product => skus.Contains(product.Sku.ToUpper()))
            .ToListAsync(cancellationToken);
        if (existing.GroupBy(product => product.Sku, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw new AppConflictException("Existing products contain SKUs that differ only in letter case. Resolve them before importing.");
        var existingBySku = existing.ToDictionary(product => product.Sku, StringComparer.OrdinalIgnoreCase);
        var created = new List<Product>();
        var updated = 0;
        foreach (var row in rows)
        {
            if (existingBySku.TryGetValue(row.Sku, out var product))
            {
                Apply(product, row);
                updated++;
            }
            else
            {
                product = new Product();
                Apply(product, row);
                database.Products.Add(product);
                created.Add(product);
            }
        }
        await database.SaveChangesAsync(cancellationToken);
        database.Inventories.AddRange(created.Select(product => new Inventory { ProductId = product.Id, Quantity = 0 }));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(created.Count, updated);
    }

    public async Task<PagedResult<CategoryDto>> GetCategoriesAsync(CatalogQuery query, CancellationToken cancellationToken)
    {
        ValidateQuery(query);
        var categories = CategoryProjection();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            categories = categories.Where(category => EF.Functions.ILike(category.Name, pattern));
        }
        categories = query.SortBy.ToLowerInvariant() switch
        {
            "name" => query.Descending ? categories.OrderByDescending(category => category.Name) : categories.OrderBy(category => category.Name),
            "createdat" => query.Descending ? categories.OrderByDescending(category => category.CreatedAt) : categories.OrderBy(category => category.CreatedAt),
            "productcount" => query.Descending ? categories.OrderByDescending(category => category.ProductCount) : categories.OrderBy(category => category.ProductCount),
            _ => throw new AppValidationException("Unsupported category sortBy. Use name, createdAt or productCount.")
        };
        var total = await categories.CountAsync(cancellationToken);
        var items = await ((IOrderedQueryable<CategoryDto>)categories).ThenBy(category => category.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return new(items, total, query.Page, query.PageSize);
    }

    public async Task<CategoryDto> GetCategoryAsync(int id, CancellationToken cancellationToken) =>
        await CategoryProjection().SingleOrDefaultAsync(category => category.Id == id, cancellationToken)
        ?? throw new AppNotFoundException("Category was not found.");

    public async Task<CategoryDto> CreateCategoryAsync(CategoryRequest request, CancellationToken cancellationToken)
    {
        NormalizeAndValidate(request);
        var category = new Category { Name = request.Name, Description = request.Description };
        database.Categories.Add(category);
        await database.SaveChangesAsync(cancellationToken);
        return await GetCategoryAsync(category.Id, cancellationToken);
    }

    public async Task<CategoryDto> UpdateCategoryAsync(int id, CategoryRequest request, CancellationToken cancellationToken)
    {
        NormalizeAndValidate(request);
        var category = await database.Categories.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new AppNotFoundException("Category was not found.");
        category.Name = request.Name;
        category.Description = request.Description;
        await database.SaveChangesAsync(cancellationToken);
        return await GetCategoryAsync(id, cancellationToken);
    }

    public async Task DeleteCategoryAsync(int id, CancellationToken cancellationToken)
    {
        var category = await database.Categories.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new AppNotFoundException("Category was not found.");
        if (await database.Products.AnyAsync(product => product.CategoryId == id, cancellationToken))
            throw new AppConflictException("Category contains products, including inactive products. Move them before deleting it.");
        database.Categories.Remove(category);
        await database.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<ProductDto> ProductProjection() => CatalogQueryFactory.Products(database);

    internal IQueryable<ProductDto> FilterProducts(CatalogQuery query)
    {
        var products = ProductProjection();
        if (!query.IncludeInactive) products = products.Where(product => product.IsActive);
        if (query.CategoryId.HasValue) products = products.Where(product => product.CategoryId == query.CategoryId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            products = products.Where(product => EF.Functions.ILike(product.Name, pattern) || EF.Functions.ILike(product.Sku, pattern));
        }
        return products;
    }

    internal static IOrderedQueryable<ProductDto> SortProducts(IQueryable<ProductDto> products, CatalogQuery query)
    {
        var sorted = query.SortBy.ToLowerInvariant() switch
        {
            "name" => query.Descending ? products.OrderByDescending(product => product.Name) : products.OrderBy(product => product.Name),
            "sku" => query.Descending ? products.OrderByDescending(product => product.Sku) : products.OrderBy(product => product.Sku),
            "price" => query.Descending ? products.OrderByDescending(product => product.Price) : products.OrderBy(product => product.Price),
            "quantity" => query.Descending ? products.OrderByDescending(product => product.Quantity) : products.OrderBy(product => product.Quantity),
            "createdat" => query.Descending ? products.OrderByDescending(product => product.CreatedAt) : products.OrderBy(product => product.CreatedAt),
            "categoryname" => query.Descending ? products.OrderByDescending(product => product.CategoryName) : products.OrderBy(product => product.CategoryName),
            "categoryid" => query.Descending ? products.OrderByDescending(product => product.CategoryId) : products.OrderBy(product => product.CategoryId),
            _ => throw new AppValidationException("Unsupported product sortBy. Use name, sku, price, quantity, createdAt, categoryName or categoryId.")
        };
        return sorted.ThenBy(product => product.Id);
    }

    private IQueryable<CategoryDto> CategoryProjection() => database.Categories.AsNoTracking()
        .Select(category => new CategoryDto
        {
            Id = category.Id, Name = category.Name, Description = category.Description, CreatedAt = category.CreatedAt,
            ProductCount = database.Products.Count(product => product.CategoryId == category.Id)
        });

    private async Task ValidateCategoryAsync(int categoryId, CancellationToken cancellationToken)
    {
        if (!await database.Categories.AnyAsync(category => category.Id == categoryId, cancellationToken))
            throw new AppValidationException("Selected category does not exist.", new Dictionary<string, string[]> { ["categoryId"] = ["Category does not exist."] });
    }

    private async Task ValidateSkuAsync(string sku, int? excludedId, CancellationToken cancellationToken)
    {
        if (await database.Products.AnyAsync(product => product.Sku.ToUpper() == sku && product.Id != excludedId, cancellationToken))
            throw new AppConflictException("A product with this SKU already exists.");
    }

    internal static void ValidateQuery(CatalogQuery query)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue)
            throw new AppValidationException("page must be positive and pageSize must be between 1 and 100.");
        if (query.CategoryId is <= 0)
            throw new AppValidationException("categoryId must be positive.");
        if (query.Search?.Length > 200)
            throw new AppValidationException("Search must be at most 200 characters.");
        query.SortBy = string.IsNullOrWhiteSpace(query.SortBy) ? "name" : query.SortBy.Trim();
    }

    private static void NormalizeAndValidate(ProductRequest request)
    {
        request.Sku = request.Sku?.Trim().ToUpperInvariant() ?? string.Empty;
        request.Name = request.Name?.Trim() ?? string.Empty;
        request.Description = request.Description?.Trim() ?? string.Empty;
        request.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
        ValidateModel(request);
        if (decimal.Round(request.Price, 2) != request.Price || decimal.Round(request.CostPrice, 2) != request.CostPrice)
            throw new AppValidationException("Prices must have at most two decimal places.");
        if (request.ImageUrl is not null && !IsAllowedImageUrl(request.ImageUrl))
            throw new AppValidationException("Image URL must use HTTP/HTTPS or an uploaded product image path.");
    }

    private static bool IsAllowedImageUrl(string value) =>
        System.Text.RegularExpressions.Regex.IsMatch(value, "^/uploads/products/[a-f0-9]{32}\\.(png|jpg|webp)$") ||
        (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo));

    private static void NormalizeAndValidate(CategoryRequest request)
    {
        request.Name = request.Name?.Trim() ?? string.Empty;
        request.Description = request.Description?.Trim() ?? string.Empty;
        ValidateModel(request);
    }

    private static void ValidateModel(object request)
    {
        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(request, new ValidationContext(request), results, true)) return;
        var errors = results.SelectMany(result => result.MemberNames.DefaultIfEmpty("request")
                .Select(member => new { Member = char.ToLowerInvariant(member[0]) + member[1..], Message = result.ErrorMessage ?? "Value is invalid." }))
            .GroupBy(result => result.Member).ToDictionary(group => group.Key, group => group.Select(result => result.Message).ToArray());
        throw new AppValidationException("Validation failed.", errors);
    }

    private static void Apply(Product product, ProductRequest request)
    {
        product.CategoryId = request.CategoryId;
        product.Sku = request.Sku;
        product.Name = request.Name;
        product.Description = request.Description;
        product.Price = request.Price;
        product.CostPrice = request.CostPrice;
        product.MinStock = request.MinStock;
        product.ImageUrl = request.ImageUrl;
        product.UpdatedAt = DateTime.UtcNow;
    }
}
