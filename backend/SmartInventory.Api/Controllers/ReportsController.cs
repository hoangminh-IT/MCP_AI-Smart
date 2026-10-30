using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Reports;

namespace SmartInventory.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Admin,Staff")]
public sealed class ReportsController(IReportService reports) : ControllerBase
{
    [HttpGet("dashboard")]
    public Task<DashboardReportDto> Dashboard([FromQuery] ReportPeriodQuery query, CancellationToken cancellationToken) =>
        reports.GetDashboardAsync(query, cancellationToken);

    [HttpGet("inventory")]
    public Task<PagedResult<InventoryReportRowDto>> Inventory([FromQuery] InventoryReportQuery query, CancellationToken cancellationToken) =>
        reports.GetInventoryAsync(query, cancellationToken);

    [HttpGet("sales")]
    public Task<PagedResult<SalesReportRowDto>> Sales([FromQuery] ReportPeriodQuery query, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        reports.GetSalesAsync(query, page, pageSize, cancellationToken);

    [HttpGet("purchases")]
    public Task<PagedResult<PurchaseReportRowDto>> Purchases([FromQuery] ReportPeriodQuery query, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        reports.GetPurchasesAsync(query, page, pageSize, cancellationToken);

    [HttpGet("stock-movements")]
    public Task<PagedResult<SmartInventory.Application.Stock.StockMovementDetailDto>> StockMovements(
        [FromQuery] StockMovementReportQuery query, CancellationToken cancellationToken) =>
        reports.GetStockMovementsAsync(query, cancellationToken);

    [HttpGet("product-performance")]
    public Task<IReadOnlyList<ProductPerformanceRowDto>> ProductPerformance([FromQuery] ReportPeriodQuery query,
        [FromQuery] int take = 20, CancellationToken cancellationToken = default) =>
        reports.GetProductPerformanceAsync(query, take, cancellationToken);

    [HttpGet("export/{report}")]
    public async Task<IActionResult> Export(string report, [FromQuery] ReportExportQuery query, CancellationToken cancellationToken)
    {
        var file = await reports.ExportAsync(report, query, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
