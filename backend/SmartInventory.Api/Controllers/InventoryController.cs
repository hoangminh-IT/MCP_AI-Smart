using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;
using SmartInventory.Application.Mcp;
using SmartInventory.Application.Stock;

namespace SmartInventory.Api.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize(Roles = "Admin,Staff")]
public sealed class InventoryController(IStockService stock) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ProductDto>> List([FromQuery] InventoryQuery query, CancellationToken cancellationToken) =>
        stock.GetInventoryAsync(query, cancellationToken);

    [HttpGet("summary")]
    public Task<InventorySummaryDto> Summary(CancellationToken cancellationToken) => stock.GetSummaryAsync(cancellationToken);

    [HttpGet("{productId:int}")]
    public Task<ProductDto> Get(int productId, CancellationToken cancellationToken) => stock.GetInventoryProductAsync(productId, cancellationToken);

    [HttpPost("receipts")]
    public Task<StockOperationDto> Receipt(StockRequest request, CancellationToken cancellationToken) =>
        stock.ReceiveAsync(CurrentUserId(), request, cancellationToken);

    [HttpPost("issues")]
    public Task<StockOperationDto> Issue(StockRequest request, CancellationToken cancellationToken) =>
        stock.IssueAsync(CurrentUserId(), request, cancellationToken);

    [HttpPost("adjustments")]
    [Authorize(Roles = "Admin")]
    public Task<StockOperationDto> Adjust(StockRequest request, CancellationToken cancellationToken) =>
        stock.AdjustAsync(CurrentUserId(), request, cancellationToken);

    private int CurrentUserId() => int.TryParse(User.FindFirstValue("sub"), NumberStyles.None,
        CultureInfo.InvariantCulture, out var id) && id > 0 ? id : throw new AppUnauthorizedException("A valid user identity is required.");
}
