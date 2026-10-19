using SmartInventory.Application.DTOs.Inventory;

namespace SmartInventory.Application.Interfaces;

/// <summary>Persistence contract. Stock changes and movement insertion must be atomic.</summary>
public interface IInventoryRepository
{
    Task<InventoryItemResponse?> GetByProductIdAsync(int productId, CancellationToken cancellationToken = default);
    Task<InventoryItemResponse?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);
    Task<PagedResponse<InventoryItemResponse>> GetInventoryAsync(InventoryQueryRequest request, CancellationToken cancellationToken = default);
    Task<PagedResponse<InventoryItemResponse>> GetLowStockAsync(LowStockQueryRequest request, CancellationToken cancellationToken = default);
    Task<InventorySummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<PagedResponse<StockMovementResponse>> GetMovementsAsync(StockMovementQueryRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the quantity change and appends its movement in one database transaction.
    /// Export must be conditional on sufficient current stock to prevent negative inventory.
    /// </summary>
    Task<StockTransactionResponse> ApplyStockMovementAsync(
        int productId, int quantity, StockMovementType type, string reason,
        CancellationToken cancellationToken = default);
}
