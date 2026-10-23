using System.ComponentModel.DataAnnotations;

namespace SmartInventory.Application.Catalog;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public sealed class CatalogQuery
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string SortBy { get; set; } = "name";
    public bool Descending { get; set; }
    public bool IncludeInactive { get; set; }
}

public sealed record ProductDto
{
    public int Id { get; init; }
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal CostPrice { get; init; }
    public int MinStock { get; init; }
    public string? ImageUrl { get; init; }
    public bool IsActive { get; init; }
    public int Quantity { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class ProductRequest
{
    [Range(1, int.MaxValue)] public int CategoryId { get; set; }
    [Required, StringLength(80)] public string Sku { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [StringLength(4000)] public string Description { get; set; } = string.Empty;
    [Range(typeof(decimal), "0", "9999999999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal Price { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal CostPrice { get; set; }
    [Range(0, int.MaxValue)] public int MinStock { get; set; } = 10;
    [StringLength(2048)] public string? ImageUrl { get; set; }
}

public sealed record CategoryDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public int ProductCount { get; init; }
}

public sealed class CategoryRequest
{
    [Required, StringLength(120)] public string Name { get; set; } = string.Empty;
    [StringLength(2000)] public string Description { get; set; } = string.Empty;
}

public sealed record ProductImportResult(int CreatedCount, int UpdatedCount);
public sealed record ProductImageResult(string ImageUrl);

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetProductsAsync(CatalogQuery query, CancellationToken cancellationToken);
    Task<ProductDto> GetProductAsync(int id, CancellationToken cancellationToken);
    Task<ProductDto> CreateProductAsync(ProductRequest request, CancellationToken cancellationToken);
    Task<ProductDto> UpdateProductAsync(int id, ProductRequest request, CancellationToken cancellationToken);
    Task DeactivateProductAsync(int id, CancellationToken cancellationToken);
    Task SetProductImageAsync(int id, string imageUrl, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductDto>> ExportProductsAsync(CatalogQuery query, CancellationToken cancellationToken);
    Task<ProductImportResult> ImportProductsAsync(IReadOnlyList<ProductRequest> rows, CancellationToken cancellationToken);
}

public interface ICategoryService
{
    Task<PagedResult<CategoryDto>> GetCategoriesAsync(CatalogQuery query, CancellationToken cancellationToken);
    Task<CategoryDto> GetCategoryAsync(int id, CancellationToken cancellationToken);
    Task<CategoryDto> CreateCategoryAsync(CategoryRequest request, CancellationToken cancellationToken);
    Task<CategoryDto> UpdateCategoryAsync(int id, CategoryRequest request, CancellationToken cancellationToken);
    Task DeleteCategoryAsync(int id, CancellationToken cancellationToken);
}

public interface IProductCsvService
{
    Task<byte[]> ExportAsync(CatalogQuery query, CancellationToken cancellationToken);
    Task<ProductImportResult> ImportAsync(Stream stream, CancellationToken cancellationToken);
}

public interface IProductImageService
{
    Task<ProductImageResult> UploadAsync(int productId, Stream stream, string fileName,
        string contentType, long length, CancellationToken cancellationToken);
}
