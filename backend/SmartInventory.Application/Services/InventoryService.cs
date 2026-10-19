using SmartInventory.Application.DTOs.Inventory;
using SmartInventory.Application.Interfaces;

namespace SmartInventory.Application.Services;

/// <summary>Application use cases and input validation for inventory operations.</summary>
public sealed class InventoryService(IInventoryRepository repository) : IInventoryService
{
    public Task<InventoryItemResponse?> GetByProductIdAsync(int productId, CancellationToken cancellationToken = default)
    {
        if (productId <= 0) throw new ArgumentOutOfRangeException(nameof(productId), "Product ID must be positive.");
        return repository.GetByProductIdAsync(productId, cancellationToken);
    }

    public Task<InventoryItemResponse?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sku)) throw new ArgumentException("SKU is required.", nameof(sku));
        return repository.GetBySkuAsync(sku.Trim(), cancellationToken);
    }

    public Task<PagedResponse<InventoryItemResponse>> GetInventoryAsync(InventoryQueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePaging(request.Page, request.PageSize);
        return repository.GetInventoryAsync(request, cancellationToken);
    }

    public Task<PagedResponse<InventoryItemResponse>> GetLowStockAsync(LowStockQueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePaging(request.Page, request.PageSize);
        return repository.GetLowStockAsync(request, cancellationToken);
    }

    public Task<InventorySummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default) =>
        repository.GetSummaryAsync(cancellationToken);

    public Task<PagedResponse<StockMovementResponse>> GetMovementsAsync(StockMovementQueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePaging(request.Page, request.PageSize);
        ValidateUtc(request.FromUtc, nameof(request.FromUtc));
        ValidateUtc(request.ToUtc, nameof(request.ToUtc));
        if (request.FromUtc.HasValue && request.ToUtc.HasValue && request.FromUtc > request.ToUtc)
            throw new ArgumentException("FromUtc must be earlier than or equal to ToUtc.", nameof(request));
        return repository.GetMovementsAsync(request, cancellationToken);
    }

    public Task<StockTransactionResponse> ImportAsync(ImportStockRequest request, CancellationToken cancellationToken = default) =>
        ApplyAsync(request, StockMovementType.Import, cancellationToken);

    public Task<StockTransactionResponse> ExportAsync(ExportStockRequest request, CancellationToken cancellationToken = default) =>
        ApplyAsync(request, StockMovementType.Export, cancellationToken);

    private Task<StockTransactionResponse> ApplyAsync(StockTransactionRequest request, StockMovementType type, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ProductId <= 0) throw new ArgumentOutOfRangeException(nameof(request.ProductId), "Product ID must be positive.");
        if (request.Quantity <= 0) throw new ArgumentOutOfRangeException(nameof(request.Quantity), "Quantity must be positive.");
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new ArgumentException("A transaction reason is required.", nameof(request));
        return repository.ApplyStockMovementAsync(request.ProductId, request.Quantity, type, request.Reason.Trim(), cancellationToken);
    }

    private static void ValidatePaging(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page), "Page must be at least 1.");
        if (pageSize is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 1 and 200.");
    }

    private static void ValidateUtc(DateTime? value, string parameterName)
    {
        if (value.HasValue && value.Value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Date filters must use UTC (DateTimeKind.Utc).", parameterName);
    }
}
