using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Application.Catalog;

namespace SmartInventory.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize(Roles = "Admin,Staff")]
public sealed class CategoriesController(ICategoryService categories) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<CategoryDto>> List([FromQuery] CatalogQuery query, CancellationToken cancellationToken) =>
        categories.GetCategoriesAsync(query, cancellationToken);

    [HttpGet("{id:int}")]
    public Task<CategoryDto> Get(int id, CancellationToken cancellationToken) => categories.GetCategoryAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CategoryDto>> Create(CategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await categories.CreateCategoryAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = category.Id }, category);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public Task<CategoryDto> Update(int id, CategoryRequest request, CancellationToken cancellationToken) =>
        categories.UpdateCategoryAsync(id, request, cancellationToken);

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await categories.DeleteCategoryAsync(id, cancellationToken);
        return NoContent();
    }
}
