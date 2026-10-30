using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Customers;

namespace SmartInventory.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize(Roles = "Admin,Staff")]
public sealed class CustomersController(ICustomerService customers) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<CustomerDto>> List([FromQuery] CustomerQuery query, CancellationToken cancellationToken) =>
        customers.GetCustomersAsync(query, cancellationToken);
    [HttpGet("{id:int}")]
    public Task<CustomerDto> Get(int id, CancellationToken cancellationToken) => customers.GetCustomerAsync(id, cancellationToken);
    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<ActionResult<CustomerDto>> Create(CustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await customers.CreateCustomerAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = customer.Id }, customer);
    }
    [HttpPut("{id:int}"), Authorize(Roles = "Admin")]
    public Task<CustomerDto> Update(int id, CustomerRequest request, CancellationToken cancellationToken) =>
        customers.UpdateCustomerAsync(id, request, cancellationToken);
    [HttpDelete("{id:int}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await customers.DeactivateCustomerAsync(id, cancellationToken);
        return NoContent();
    }
}
