using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Application.Catalog;

namespace SmartInventory.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize(Roles = "Admin,Staff")]
public sealed class ProductsController(IProductService products, IProductCsvService csv,
    IProductImageService images) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ProductDto>> List([FromQuery] CatalogQuery query, CancellationToken cancellationToken) =>
        products.GetProductsAsync(query, cancellationToken);

    [HttpGet("{id:int}")]
    public Task<ProductDto> Get(int id, CancellationToken cancellationToken) => products.GetProductAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductDto>> Create(ProductRequest request, CancellationToken cancellationToken)
    {
        var product = await products.CreateProductAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public Task<ProductDto> Update(int id, ProductRequest request, CancellationToken cancellationToken) =>
        products.UpdateProductAsync(id, request, cancellationToken);

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await products.DeactivateProductAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] CatalogQuery query, CancellationToken cancellationToken) =>
        File(await csv.ExportAsync(query, cancellationToken), "text/csv; charset=utf-8", "products.csv");

    [HttpPost("import")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<ProductImportResult> Import([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return await csv.ImportAsync(stream, cancellationToken);
    }

    [HttpPost("{id:int}/image")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ProductImageResult> Upload(int id, [FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return await images.UploadAsync(id, stream, file.FileName, file.ContentType, file.Length, cancellationToken);
    }
}
