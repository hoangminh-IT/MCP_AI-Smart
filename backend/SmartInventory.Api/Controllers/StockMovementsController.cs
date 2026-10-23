using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Stock;

namespace SmartInventory.Api.Controllers;

[ApiController]
[Route("api/stock-movements")]
[Authorize(Roles = "Admin,Staff")]
public sealed class StockMovementsController(IStockService stock) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<StockMovementDetailDto>> List([FromQuery] StockMovementQuery query, CancellationToken cancellationToken) =>
        stock.GetMovementsAsync(query, cancellationToken);
}
