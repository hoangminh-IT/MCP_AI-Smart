using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Application.Audit;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;

namespace SmartInventory.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = "Admin")]
public sealed class AuditLogsController(IAuditService audit) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AuditEntryDto>> List([FromQuery] AuditQuery query, CancellationToken cancellationToken) =>
        audit.GetAuditAsync(CurrentUserId(), query, cancellationToken);

    private int CurrentUserId() => int.TryParse(User.FindFirstValue("sub"), NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
        ? id : throw new AppUnauthorizedException("The token identity is invalid.");
}
