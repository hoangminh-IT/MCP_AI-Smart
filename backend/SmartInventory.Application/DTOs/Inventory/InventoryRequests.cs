using System.ComponentModel.DataAnnotations;

namespace SmartInventory.Application.DTOs.Inventory;

/// <summary>Optional filters and paging for inventory lookup.</summary>
public sealed record InventoryQueryRequest
{
    public string? Search { get; init; }
    public bool? LowStockOnly { get; init; }
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 200)] public int PageSize { get; init; } = 20;
}

/// <summary>Identifies one product by database ID or SKU.</summary>
public sealed record InventoryLookupRequest
{
    [Range(1, int.MaxValue)] public int? ProductId { get; init; }
    [StringLength(100)] public string? Sku { get; init; }
}

public abstract record StockTransactionRequest
{
    [Range(1, int.MaxValue)] public int ProductId { get; init; }
    [Range(1, int.MaxValue)] public int Quantity { get; init; }
    [Required, StringLength(500)] public string Reason { get; init; } = string.Empty;
}

public sealed record ImportStockRequest : StockTransactionRequest;
public sealed record ExportStockRequest : StockTransactionRequest;

public sealed record LowStockQueryRequest
{
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 200)] public int PageSize { get; init; } = 20;
}

public sealed record StockMovementQueryRequest
{
    [Range(1, int.MaxValue)] public int? ProductId { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 200)] public int PageSize { get; init; } = 20;
}
