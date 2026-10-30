using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Suppliers;

namespace SmartInventory.Api.Controllers;

[ApiController]
[Route("api/suppliers")]
[Authorize(Roles = "Admin,Staff")]
public sealed class SuppliersController(ISupplierService suppliers) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<SupplierDto>> List([FromQuery] SupplierQuery query, CancellationToken cancellationToken) =>
        suppliers.GetSuppliersAsync(query, cancellationToken);

    [HttpGet("{id:int}")]
    public Task<SupplierDto> Get(int id, CancellationToken cancellationToken) => suppliers.GetSupplierAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SupplierDto>> Create(SupplierRequest request, CancellationToken cancellationToken)
    {
        var supplier = await suppliers.CreateSupplierAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = supplier.Id }, supplier);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public Task<SupplierDto> Update(int id, SupplierRequest request, CancellationToken cancellationToken) =>
        suppliers.UpdateSupplierAsync(id, request, cancellationToken);

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await suppliers.DeactivateSupplierAsync(id, cancellationToken);
        return NoContent();
    }
}
