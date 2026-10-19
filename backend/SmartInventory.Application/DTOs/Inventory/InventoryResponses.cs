namespace SmartInventory.Application.DTOs.Inventory;

public enum InventoryStatus { InStock, LowStock, OutOfStock }
public enum StockMovementType { Import, Export }
public enum StockTransactionOutcome { Applied, ProductNotFound, InsufficientStock }

public sealed record InventoryItemResponse(
    int ProductId, string Sku, string Name, int Quantity, int MinStock, InventoryStatus Status);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount);

public sealed record StockTransactionResponse(
    StockTransactionOutcome Outcome, int ProductId, int? QuantityBefore,
    int? QuantityAfter, string? Message = null);

public sealed record InventorySummaryResponse(
    int ProductCount, long TotalQuantity, int LowStockProductCount,
    int OutOfStockProductCount);

public sealed record StockMovementResponse(
    int Id, int ProductId, string Sku, string ProductName,
    StockMovementType Type, int Quantity, string Reason, DateTime CreatedAtUtc);
