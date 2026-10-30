using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;
using SmartInventory.Application.Purchasing;

namespace SmartInventory.Api.Controllers;

[ApiController]
[Route("api/purchase-orders")]
[Authorize(Roles = "Admin,Staff")]
public sealed class PurchaseOrdersController(IPurchaseOrderService orders) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<PurchaseOrderSummaryDto>> List([FromQuery] PurchaseOrderQuery query, CancellationToken cancellationToken) =>
        orders.ListAsync(query, cancellationToken);

    [HttpGet("{id:int}")]
    public Task<PurchaseOrderDetailDto> Get(int id, CancellationToken cancellationToken) => orders.GetAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<PurchaseOrderDetailDto>> Create(PurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await orders.CreateAsync(CurrentUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    [HttpPut("{id:int}")]
    public Task<PurchaseOrderDetailDto> Update(int id, PurchaseOrderRequest request, CancellationToken cancellationToken) =>
        orders.UpdateAsync(CurrentUserId(), id, request, cancellationToken);

    [HttpPost("{id:int}/submit")]
    public Task<PurchaseOrderDetailDto> Submit(int id, CancellationToken cancellationToken) => orders.SubmitAsync(CurrentUserId(), id, cancellationToken);

    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = "Admin")]
    public Task<PurchaseOrderDetailDto> Approve(int id, CancellationToken cancellationToken) => orders.ApproveAsync(CurrentUserId(), id, cancellationToken);

    [HttpPost("{id:int}/cancel")]
    public Task<PurchaseOrderDetailDto> Cancel(int id, CancellationToken cancellationToken) => orders.CancelAsync(CurrentUserId(), id, cancellationToken);

    [HttpPost("{id:int}/receipts")]
    public Task<PurchaseReceiptResult> Receive(int id, PurchaseReceiptRequest request, CancellationToken cancellationToken) =>
        orders.ReceiveAsync(CurrentUserId(), id, request, cancellationToken);

    private int CurrentUserId() => int.TryParse(User.FindFirstValue("sub"), NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
        ? id : throw new AppUnauthorizedException("A valid user identity is required.");
}
