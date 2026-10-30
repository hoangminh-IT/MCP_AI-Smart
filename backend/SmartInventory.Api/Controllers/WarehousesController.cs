using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;
using SmartInventory.Application.Warehouses;

namespace SmartInventory.Api.Controllers;

[ApiController, Route("api/warehouses"), Authorize(Roles = "Admin,Staff")]
public sealed class WarehousesController(IWarehouseService warehouses) : ControllerBase
{
    [HttpGet] public Task<PagedResult<WarehouseDto>> List([FromQuery] WarehouseQuery query, CancellationToken cancellationToken) => warehouses.GetWarehousesAsync(query, cancellationToken);
    [HttpGet("{id:int}")] public Task<WarehouseDto> Get(int id, CancellationToken cancellationToken) => warehouses.GetWarehouseAsync(id, cancellationToken);
    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<ActionResult<WarehouseDto>> Create(WarehouseRequest request, CancellationToken cancellationToken)
    {
        var result = await warehouses.CreateAsync(Actor(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }
    [HttpPut("{id:int}"), Authorize(Roles = "Admin")]
    public Task<WarehouseDto> Update(int id, WarehouseRequest request, CancellationToken cancellationToken) => warehouses.UpdateAsync(Actor(), id, request, cancellationToken);
    [HttpDelete("{id:int}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    { await warehouses.DeactivateAsync(Actor(), id, cancellationToken); return NoContent(); }
    private int Actor() => int.TryParse(User.FindFirstValue("sub"), NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
        ? id : throw new AppUnauthorizedException("A valid identity is required.");
}
