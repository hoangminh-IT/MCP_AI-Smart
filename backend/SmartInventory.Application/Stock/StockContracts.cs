using System.ComponentModel.DataAnnotations;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Mcp;

namespace SmartInventory.Application.Stock;

public sealed class StockRequest
{
    [Range(1, int.MaxValue)] public int ProductId { get; set; }
    // Receipt/issue require a positive amount; adjustment treats this value as the target quantity.
    [Range(0, int.MaxValue)] public int Quantity { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    public Guid RequestId { get; set; }
}

public sealed class InventoryQuery
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string SortBy { get; set; } = "name";
    public bool Descending { get; set; }
    public bool IncludeInactive { get; set; }
    public string StockState { get; set; } = "all";
}

public sealed class StockMovementQuery
{
    public int? ProductId { get; set; }
    public string? Type { get; set; }
    // Strings retain the caller's timezone designator so the service can enforce explicit UTC.
    public string? From { get; set; }
    public string? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public sealed record StockOperationDto(int MovementId, int ProductId, string Type, int Quantity,
    int QuantityBefore, int QuantityAfter, string Reason, int CreatedBy, DateTime CreatedAt,
    Guid RequestId, bool Replayed);

public sealed record StockMovementDetailDto(int Id, int ProductId, string Sku, string ProductName,
    string Type, int Quantity, int? QuantityBefore, int? QuantityAfter, string Reason, int? CreatedBy,
    DateTime CreatedAt, Guid? RequestId, string? ReferenceType, string? ReferenceId);

public static class StockMovementTypes
{
    public const string Receipt = "Receipt";
    public const string Issue = "Issue";
    public const string Adjustment = "Adjustment";
}

public interface IStockService
{
    Task<StockOperationDto> ReceiveAsync(int userId, StockRequest request, CancellationToken cancellationToken);
    Task<StockOperationDto> IssueAsync(int userId, StockRequest request, CancellationToken cancellationToken);
    Task<StockOperationDto> AdjustAsync(int userId, StockRequest request, CancellationToken cancellationToken);
    Task<PagedResult<ProductDto>> GetInventoryAsync(InventoryQuery query, CancellationToken cancellationToken);
    Task<ProductDto> GetInventoryProductAsync(int productId, CancellationToken cancellationToken);
    Task<InventorySummaryDto> GetSummaryAsync(CancellationToken cancellationToken);
    Task<PagedResult<StockMovementDetailDto>> GetMovementsAsync(StockMovementQuery query, CancellationToken cancellationToken);
}
