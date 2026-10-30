using System.Text.Json;
using SmartInventory.Application.Catalog;

namespace SmartInventory.Application.Mcp;

public sealed record McpToolDescriptor(string Name, string? Description, JsonElement InputSchema, JsonElement? Annotations);
public sealed record McpConnectionStatus(bool Connected, string Transport, string ServerUrl,
    string? ServerName, string? ServerVersion, string? ErrorMessage, DateTime CheckedAt);
public sealed record McpExecutionDto(int ExecutionId, string ToolName, JsonElement Input, JsonElement? Output,
    bool IsError, long DurationMs, DateTime CreatedAt, string? ErrorMessage, string Status);

/// <summary>Uses the official C# SDK to contact the configured first-party MCP resource as the current caller.</summary>
public interface IMcpClientService
{
    Task<McpConnectionStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken = default);
    Task<McpExecutionDto> ExecuteToolAsync(string toolName, JsonElement arguments, CancellationToken cancellationToken = default);
}

public interface IMcpExecutionLogService
{
    Task<int> AppendAsync(int userId, string toolName, JsonElement input, JsonElement? output,
        string status, long durationMs, string? errorMessage, CancellationToken cancellationToken = default);
    Task<PagedResult<McpExecutionDto>> GetHistoryAsync(int userId, int page, int pageSize,
        CancellationToken cancellationToken = default);
    Task<McpExecutionDto> GetAsync(int userId, int executionId, CancellationToken cancellationToken = default);
}

public sealed record InventorySummaryDto(int TotalProducts, int TotalCategories, long TotalQuantity,
    decimal InventoryCostValue, decimal InventoryRetailValue, int LowStockCount, int OutOfStockCount,
    long StockMovementCount, DateTime AsOf, int? WarehouseId = null, string? WarehouseCode = null, string? WarehouseName = null);
public sealed record StockMovementDto(int Id, int ProductId, string Sku, string ProductName,
    string Type, int Quantity, DateTime CreatedAt, int? QuantityBefore = null, int? QuantityAfter = null,
    string? Reason = null, int? CreatedBy = null, Guid? RequestId = null, string? ReferenceType = null, string? ReferenceId = null, int WarehouseId = 1, string? WarehouseCode = null,
    string? WarehouseName = null, int? WarehouseQuantityBefore = null, int? WarehouseQuantityAfter = null);
public sealed record CategoryInventoryDto(int CategoryId, string Name, int ProductCount, long TotalQuantity,
    decimal InventoryCostValue, decimal InventoryRetailValue);

public interface IInventoryReadService
{
    Task<ProductDto> GetProductBySkuAsync(string sku, CancellationToken cancellationToken, int? warehouseId = null);
    Task<PagedResult<ProductDto>> GetLowStockProductsAsync(int? threshold, int page, int pageSize, CancellationToken cancellationToken, int? warehouseId = null);
    Task<PagedResult<ProductDto>> GetOutOfStockProductsAsync(int page, int pageSize, CancellationToken cancellationToken, int? warehouseId = null);
    Task<InventorySummaryDto> GetInventorySummaryAsync(CancellationToken cancellationToken, int? warehouseId = null);
    Task<PagedResult<StockMovementDto>> GetStockMovementHistoryAsync(int? productId, int page, int pageSize, CancellationToken cancellationToken, int? warehouseId = null);
    Task<IReadOnlyList<CategoryInventoryDto>> GetCategorySummaryAsync(CancellationToken cancellationToken, int? warehouseId = null);
}

public static class McpExecutionStatuses
{
    public const string Succeeded = "Succeeded";
    public const string ToolError = "ToolError";
    public const string ProtocolError = "ProtocolError";
    public const string TransportError = "TransportError";
    public const string Timeout = "Timeout";
    public const string Cancelled = "Cancelled";
    public const string ValidationError = "ValidationError";
}
