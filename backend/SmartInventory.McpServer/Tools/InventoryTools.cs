using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;
using SmartInventory.Application.Mcp;

namespace SmartInventory.McpServer.Tools;

/// <summary>Read-only MCP adapters to the same PostgreSQL business services used by the REST API.</summary>
public sealed class InventoryTools
{
    private const string ReaderRoles = UserRoles.Admin + "," + UserRoles.Staff;

    [Authorize(Roles = ReaderRoles)]
    [McpServerTool(Name = "GetAllProducts", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("List active inventory products with current quantities. Uses server-side pagination, at most 100 products per page.")]
    public static Task<PagedResult<ProductDto>> GetAllProducts(
        IProductService products,
        [Description("Page number, starting at 1."), Range(1, int.MaxValue)] int page = 1,
        [Description("Products per page, between 1 and 100."), Range(1, 100)] int pageSize = 10,
        CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        ValidatePaging(page, pageSize);
        return await products.GetProductsAsync(new CatalogQuery { Page = page, PageSize = pageSize }, cancellationToken);
    });

    [Authorize(Roles = ReaderRoles)]
    [McpServerTool(Name = "GetProductById", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Read one active product by its numeric identifier, including current stock quantity.")]
    public static Task<ProductDto> GetProductById(
        IProductService products,
        [Description("Positive product identifier."), Range(1, int.MaxValue)] int id,
        CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        if (id <= 0) throw new AppValidationException("id must be positive.");
        var product = await products.GetProductAsync(id, cancellationToken);
        if (!product.IsActive) throw new AppNotFoundException("Product was not found.");
        return product;
    });

    [Authorize(Roles = ReaderRoles)]
    [McpServerTool(Name = "GetProductBySku", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Read one active product by its SKU and return its current inventory quantity.")]
    public static Task<ProductDto> GetProductBySku(
        IInventoryReadService inventory,
        [Description("Nonempty product SKU, up to 80 characters."), Required, MaxLength(80)] string sku,
        CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(sku) || sku.Length > 80)
            throw new AppValidationException("sku must contain 1 to 80 characters.");
        return await inventory.GetProductBySkuAsync(sku.Trim(), cancellationToken);
    });

    [Authorize(Roles = ReaderRoles)]
    [McpServerTool(Name = "SearchProducts", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Search active product names and SKUs, returning a page of real inventory data.")]
    public static Task<PagedResult<ProductDto>> SearchProducts(
        IProductService products,
        [Description("Nonempty product name or SKU search text, up to 200 characters."), Required, MaxLength(200)] string search,
        [Description("Page number, starting at 1."), Range(1, int.MaxValue)] int page = 1,
        [Description("Products per page, between 1 and 100."), Range(1, 100)] int pageSize = 10,
        CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(search) || search.Length > 200)
            throw new AppValidationException("search must contain 1 to 200 characters.");
        ValidatePaging(page, pageSize);
        return await products.GetProductsAsync(new CatalogQuery
        {
            Search = search.Trim(), Page = page, PageSize = pageSize
        }, cancellationToken);
    });

    [Authorize(Roles = ReaderRoles)]
    [McpServerTool(Name = "GetProductsByCategory", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("List active products in a category, with current quantities and server-side pagination.")]
    public static Task<PagedResult<ProductDto>> GetProductsByCategory(
        IProductService products,
        [Description("Positive category identifier."), Range(1, int.MaxValue)] int categoryId,
        [Description("Page number, starting at 1."), Range(1, int.MaxValue)] int page = 1,
        [Description("Products per page, between 1 and 100."), Range(1, 100)] int pageSize = 10,
        CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        if (categoryId <= 0) throw new AppValidationException("categoryId must be positive.");
        ValidatePaging(page, pageSize);
        return await products.GetProductsAsync(new CatalogQuery
        {
            CategoryId = categoryId, Page = page, PageSize = pageSize
        }, cancellationToken);
    });

    [Authorize(Roles = ReaderRoles)]
    [McpServerTool(Name = "GetLowStockProducts", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("List active products whose quantity is strictly below threshold. Omit threshold to use each product's minStock; threshold 0 returns no nonnegative inventory.")]
    public static Task<PagedResult<ProductDto>> GetLowStockProducts(
        IInventoryReadService inventory,
        [Description("Optional nonnegative threshold. Products must have quantity strictly less than this number."), Range(0, int.MaxValue)] int? threshold = null,
        [Description("Page number, starting at 1."), Range(1, int.MaxValue)] int page = 1,
        [Description("Products per page, between 1 and 100."), Range(1, 100)] int pageSize = 10,
        CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        if (threshold < 0) throw new AppValidationException("threshold must not be negative.");
        ValidatePaging(page, pageSize);
        return await inventory.GetLowStockProductsAsync(threshold, page, pageSize, cancellationToken);
    });

    [Authorize(Roles = ReaderRoles)]
    [McpServerTool(Name = "GetOutOfStockProducts", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("List active products with zero stock, using server-side pagination.")]
    public static Task<PagedResult<ProductDto>> GetOutOfStockProducts(
        IInventoryReadService inventory,
        [Description("Page number, starting at 1."), Range(1, int.MaxValue)] int page = 1,
        [Description("Products per page, between 1 and 100."), Range(1, 100)] int pageSize = 10,
        CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        ValidatePaging(page, pageSize);
        return await inventory.GetOutOfStockProductsAsync(page, pageSize, cancellationToken);
    });

    [Authorize(Roles = ReaderRoles)]
    [McpServerTool(Name = "GetInventorySummary", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Get active-product counts, total quantity, inventory cost and retail valuation, stock warning counts and a UTC observation timestamp.")]
    public static Task<InventorySummaryDto> GetInventorySummary(
        IInventoryReadService inventory, CancellationToken cancellationToken = default) =>
        SafeAsync(() => inventory.GetInventorySummaryAsync(cancellationToken));

    [Authorize(Roles = ReaderRoles)]
    [McpServerTool(Name = "GetStockMovementHistory", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Read recorded stock movements, newest first, optionally restricted to one product. Does not change inventory.")]
    public static Task<PagedResult<StockMovementDto>> GetStockMovementHistory(
        IInventoryReadService inventory,
        [Description("Optional positive product identifier."), Range(1, int.MaxValue)] int? productId = null,
        [Description("Page number, starting at 1."), Range(1, int.MaxValue)] int page = 1,
        [Description("Movements per page, between 1 and 100."), Range(1, 100)] int pageSize = 10,
        CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        if (productId <= 0) throw new AppValidationException("productId must be positive when provided.");
        ValidatePaging(page, pageSize);
        return await inventory.GetStockMovementHistoryAsync(productId, page, pageSize, cancellationToken);
    });

    [Authorize(Roles = ReaderRoles)]
    [McpServerTool(Name = "GetCategorySummary", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Summarize each category's active products, total quantity, cost value and retail inventory value.")]
    public static Task<IReadOnlyList<CategoryInventoryDto>> GetCategorySummary(
        IInventoryReadService inventory, CancellationToken cancellationToken = default) =>
        SafeAsync(() => inventory.GetCategorySummaryAsync(cancellationToken));

    private static void ValidatePaging(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new AppValidationException("page must be positive and pageSize must be between 1 and 100.");
    }

    private static async Task<T> SafeAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (AppValidationException exception)
        {
            // Only deliberate application messages may be sent to clients; SDK hides unexpected failures.
            throw new McpException(exception.Message);
        }
        catch (AppNotFoundException exception)
        {
            throw new McpException(exception.Message);
        }
        catch (AppConflictException exception)
        {
            throw new McpException(exception.Message);
        }
    }
}
