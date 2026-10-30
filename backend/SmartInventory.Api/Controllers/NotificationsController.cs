using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;
using SmartInventory.Application.Notifications;

namespace SmartInventory.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize(Roles = "Admin,Staff")]
public sealed class NotificationsController(INotificationService notifications) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<NotificationDto>> List([FromQuery] NotificationQuery query, CancellationToken cancellationToken) =>
        notifications.ListAsync(CurrentUserId(), query, cancellationToken);

    [HttpGet("summary")]
    public Task<NotificationSummaryDto> Summary(CancellationToken cancellationToken) =>
        notifications.GetSummaryAsync(CurrentUserId(), cancellationToken);

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        await notifications.MarkReadAsync(CurrentUserId(), id, cancellationToken);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await notifications.MarkAllReadAsync(CurrentUserId(), cancellationToken);
        return NoContent();
    }

    private int CurrentUserId() => int.TryParse(User.FindFirstValue("sub"), NumberStyles.None,
        CultureInfo.InvariantCulture, out var id) && id > 0 ? id : throw new AppUnauthorizedException("A valid user identity is required.");
}
