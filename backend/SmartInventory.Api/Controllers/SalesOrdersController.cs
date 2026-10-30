using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;
using SmartInventory.Application.Sales;

namespace SmartInventory.Api.Controllers;

[ApiController]
[Route("api/sales-orders")]
[Authorize(Roles = "Admin,Staff")]
public sealed class SalesOrdersController(ISalesOrderService orders) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<SalesOrderSummaryDto>> List([FromQuery] SalesOrderQuery query, CancellationToken cancellationToken) =>
        orders.ListAsync(query, cancellationToken);

    [HttpGet("{id:int}")]
    public Task<SalesOrderDetailDto> Get(int id, CancellationToken cancellationToken) => orders.GetAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<SalesOrderDetailDto>> Create(SalesOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await orders.CreateAsync(CurrentUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    [HttpPut("{id:int}")]
    public Task<SalesOrderDetailDto> Update(int id, SalesOrderRequest request, CancellationToken cancellationToken) =>
        orders.UpdateAsync(CurrentUserId(), id, request, cancellationToken);

    [HttpPost("{id:int}/confirm")]
    public Task<SalesOrderDetailDto> Confirm(int id, CancellationToken cancellationToken) =>
        orders.ConfirmAsync(CurrentUserId(), id, cancellationToken);

    [HttpPost("{id:int}/cancel")]
    public Task<SalesOrderDetailDto> Cancel(int id, CancellationToken cancellationToken) =>
        orders.CancelAsync(CurrentUserId(), id, cancellationToken);

    [HttpPost("{id:int}/fulfillments")]
    public Task<SalesFulfillmentResult> Fulfill(int id, SalesFulfillmentRequest request, CancellationToken cancellationToken) =>
        orders.FulfillAsync(CurrentUserId(), id, request, cancellationToken);

    private int CurrentUserId() => int.TryParse(User.FindFirstValue("sub"), NumberStyles.None,
        CultureInfo.InvariantCulture, out var id) && id > 0 ? id : throw new AppUnauthorizedException("A valid user identity is required.");
}
