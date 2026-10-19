using SmartInventory.Application.DTOs.Inventory;

namespace SmartInventory.Application.Interfaces;

/// <summary>Validates inventory use cases and delegates persistence to the repository.</summary>
public interface IInventoryService
{
    Task<InventoryItemResponse?> GetByProductIdAsync(int productId, CancellationToken cancellationToken = default);
    Task<InventoryItemResponse?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);
    Task<PagedResponse<InventoryItemResponse>> GetInventoryAsync(InventoryQueryRequest request, CancellationToken cancellationToken = default);
    Task<PagedResponse<InventoryItemResponse>> GetLowStockAsync(LowStockQueryRequest request, CancellationToken cancellationToken = default);
    Task<InventorySummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<PagedResponse<StockMovementResponse>> GetMovementsAsync(StockMovementQueryRequest request, CancellationToken cancellationToken = default);
    Task<StockTransactionResponse> ImportAsync(ImportStockRequest request, CancellationToken cancellationToken = default);
    Task<StockTransactionResponse> ExportAsync(ExportStockRequest request, CancellationToken cancellationToken = default);
}
