using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using SmartInventory.Application.Common;
using SmartInventory.Application.Mcp;
using SmartInventory.Application.Purchasing;
using SmartInventory.Application.Stock;
using static SmartInventory.McpServer.Tools.InventoryTools;

namespace SmartInventory.McpServer.Tools;

/// <summary>Business adapters protected by the server's confirmation guard before any SDK tool invocation.</summary>
public sealed class WriteTools
{
    private const string Operators = "Admin,Staff";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Authorize(Roles = Operators)]
    [McpServerTool(Name = "CreateStockReceipt", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Receive stock through the shared transaction service. REQUIRES explicit user approval of a persisted write intent; the server supplies a private confirmation header. Proposing this tool never authorizes execution. requestId is filled during intent preparation when omitted.")]
    public static Task<StockOperationDto> CreateStockReceipt(IStockService stock, ICurrentActor actor,
        [Range(1, int.MaxValue)] int productId, [Range(1, int.MaxValue)] int quantity,
        [Required, MaxLength(1000)] string reason, Guid requestId = default, [Range(1, int.MaxValue)] int? warehouseId = null, CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        var input = StockArguments("CreateStockReceipt", productId, quantity, reason, requestId, warehouseId);
        return await stock.ReceiveAsync(UserId(actor), input, cancellationToken);
    });

    [Authorize(Roles = Operators)]
    [McpServerTool(Name = "CreateStockIssue", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Issue stock with concurrency and insufficient-stock protection. Requires explicit user approval and the private server confirmation header. requestId is filled during intent preparation when omitted.")]
    public static Task<StockOperationDto> CreateStockIssue(IStockService stock, ICurrentActor actor,
        [Range(1, int.MaxValue)] int productId, [Range(1, int.MaxValue)] int quantity,
        [Required, MaxLength(1000)] string reason, Guid requestId = default, [Range(1, int.MaxValue)] int? warehouseId = null, CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        var input = StockArguments("CreateStockIssue", productId, quantity, reason, requestId, warehouseId);
        return await stock.IssueAsync(UserId(actor), input, cancellationToken);
    });

    [Authorize(Roles = "Admin")]
    [McpServerTool(Name = "AdjustInventory", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Admin only: set a product's TARGET stock quantity to a nonnegative integer, recording the actual before/after movement. Requires explicit approval and private confirmation; quantity is the target, not a delta.")]
    public static Task<StockOperationDto> AdjustInventory(IStockService stock, ICurrentActor actor,
        [Range(1, int.MaxValue)] int productId, [Range(0, int.MaxValue)] int quantity,
        [Required, MaxLength(1000)] string reason, Guid requestId = default, [Range(1, int.MaxValue)] int? warehouseId = null, CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        var input = StockArguments("AdjustInventory", productId, quantity, reason, requestId, warehouseId);
        return await stock.AdjustAsync(UserId(actor), input, cancellationToken);
    });

    [Authorize(Roles = Operators)]
    [McpServerTool(Name = "CreatePurchaseOrder", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Create a Draft purchase order with 1 to 100 unique product lines and agreed unit costs; no inventory is received. Explicit user approval and the private confirmation header are required; a repeated intent returns its original order.")]
    public static Task<PurchaseOrderDetailDto> CreatePurchaseOrder(IPurchaseOrderService orders, ICurrentActor actor,
        [Range(1, int.MaxValue)] int supplierId, [Required, MinLength(1), MaxLength(100)] List<PurchaseItemRequest> items,
        [MaxLength(2000)] string? notes = null, [Range(1, int.MaxValue)] int? warehouseId = null, CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        var canonical = McpWritePayload.Normalize("CreatePurchaseOrder", JsonSerializer.SerializeToElement(new { supplierId, items, notes, warehouseId }, JsonOptions));
        var request = canonical.Deserialize<PurchaseOrderRequest>(JsonOptions) ?? throw new AppValidationException("Invalid purchase order payload.");
        return await orders.CreateAsync(UserId(actor), request, cancellationToken);
    });

    [Authorize(Roles = Operators)]
    [McpServerTool(Name = "UpdatePurchaseOrderStatus", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Transition a purchase order to Submitted, Approved (Admin only), or Cancelled through existing business rules. Cannot set receipt states or receive goods. Requires explicit user approval and private confirmation.")]
    public static Task<PurchaseOrderDetailDto> UpdatePurchaseOrderStatus(IPurchaseOrderService orders, ICurrentActor actor,
        [Range(1, int.MaxValue)] int id, [Required, MaxLength(32)] string status,
        CancellationToken cancellationToken = default) => SafeAsync(async () =>
    {
        var canonical = McpWritePayload.Normalize("UpdatePurchaseOrderStatus", JsonSerializer.SerializeToElement(new { id, status }, JsonOptions));
        return canonical.GetProperty("status").GetString() switch
        {
            PurchaseStatuses.Submitted => await orders.SubmitAsync(UserId(actor), id, cancellationToken),
            PurchaseStatuses.Approved => await orders.ApproveAsync(UserId(actor), id, cancellationToken),
            _ => await orders.CancelAsync(UserId(actor), id, cancellationToken)
        };
    });

    private static StockRequest StockArguments(string tool, int productId, int quantity, string reason, Guid requestId, int? warehouseId)
    {
        var canonical = McpWritePayload.Normalize(tool, JsonSerializer.SerializeToElement(new { productId, quantity, reason, requestId, warehouseId }, JsonOptions));
        return canonical.Deserialize<StockRequest>(JsonOptions) ?? throw new AppValidationException("Invalid stock payload.");
    }

    private static int UserId(ICurrentActor actor) => actor.UserId is > 0 ? actor.UserId.Value :
        throw new AppUnauthorizedException("An authenticated user is required.");
}
