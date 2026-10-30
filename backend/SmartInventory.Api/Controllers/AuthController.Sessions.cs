using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Common;

namespace SmartInventory.Api.Controllers;

public sealed partial class AuthController
{
    private const string RefreshCookie = "inventory.refresh";

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("refresh")]
    public async Task<ActionResult<LoginResponse>> Refresh(BrowserSessionRequest request, CancellationToken cancellationToken)
    {
        RequireBrowserSessionHeader();
        try
        {
            var session = await authService.RefreshAsync(Request.Cookies[RefreshCookie] ?? "", ExpectedSessionUser(request), cancellationToken);
            SetRefreshCookie(session);
            return Ok(session.Login);
        }
        catch (AppUnauthorizedException exception)
        {
            DeleteRefreshCookie();
            // Throwing here would let exception middleware clear Set-Cookie before writing 401.
            // Return the same safe ProblemDetails while preserving credential deletion.
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized", detail: exception.Message);
        }
    }

    [HttpPost("session/logout")]
    [AllowAnonymous]
    [EnableRateLimiting("refresh")]
    public async Task<IActionResult> SessionLogout(BrowserSessionRequest request, CancellationToken cancellationToken)
    {
        RequireBrowserSessionHeader();
        var expectedUserId = ExpectedSessionUser(request);
        var revoked = await authService.LogoutSessionAsync(Request.Cookies[RefreshCookie] ?? "", expectedUserId, cancellationToken);
        // The cookie may have expired while its last access token is still valid. Explicit logout
        // must revoke that token too; a missing/expired bearer still permits cookie-only logout.
        if (!revoked && User.Identity?.IsAuthenticated == true)
            await authService.LogoutAsync(CurrentUserId(), cancellationToken);
        DeleteRefreshCookie();
        return NoContent();
    }

    private int? ExpectedSessionUser(BrowserSessionRequest request)
    {
        var bearerUserId = User.Identity?.IsAuthenticated == true ? CurrentUserId() : (int?)null;
        if (bearerUserId.HasValue && request.ExpectedUserId.HasValue && bearerUserId != request.ExpectedUserId)
            throw new AppConflictException("The browser session belongs to another account. Sign in again.");
        return request.ExpectedUserId ?? bearerUserId;
    }

    private async Task<bool> CanClearRefreshCookieAsync(int userId, CancellationToken cancellationToken)
    {
        var owner = await authService.GetRefreshSessionOwnerAsync(Request.Cookies[RefreshCookie] ?? "", cancellationToken);
        return !owner.HasValue || owner == userId;
    }

    private void RequireBrowserSessionHeader()
    {
        // A fixed custom header forces browser CORS preflight. Only the exact configured origins
        // are trusted by API CORS; production UI uses the same-origin reverse proxy.
        if (Request.Headers["X-Inventory-Session"].ToString() != "1" ||
            string.Equals(Request.Headers["Sec-Fetch-Site"].ToString(), "cross-site", StringComparison.OrdinalIgnoreCase))
            throw new AppForbiddenException("A same-origin browser session request is required.");
    }

    private CookieOptions RefreshCookieOptions() => new()
    {
        HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/api/auth", IsEssential = true,
        Secure = Request.IsHttps || (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
    };

    private void SetRefreshCookie(AuthSession session)
    {
        var options = RefreshCookieOptions();
        options.Expires = session.RefreshExpiresAt;
        options.MaxAge = session.RefreshExpiresAt - DateTime.UtcNow;
        Response.Cookies.Append(RefreshCookie, session.RefreshToken, options);
    }

    private void DeleteRefreshCookie() => Response.Cookies.Delete(RefreshCookie, RefreshCookieOptions());
}
