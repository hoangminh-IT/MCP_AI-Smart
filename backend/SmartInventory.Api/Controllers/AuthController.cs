using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Common;

namespace SmartInventory.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request,
        CancellationToken cancellationToken) =>
        Ok(await authService.LoginAsync(request, cancellationToken));

    [HttpGet("me")]
    public async Task<ActionResult<UserProfile>> Me(CancellationToken cancellationToken) =>
        Ok(await authService.GetProfileAsync(CurrentUserId(), cancellationToken));

    [HttpPut("profile")]
    public async Task<ActionResult<UserProfile>> UpdateProfile(UpdateProfileRequest request,
        CancellationToken cancellationToken) =>
        Ok(await authService.UpdateProfileAsync(CurrentUserId(), request, cancellationToken));

    [HttpPost("change-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        await authService.ChangePasswordAsync(CurrentUserId(), request, cancellationToken);
        return NoContent();
    }

    /// <summary>Revokes all access tokens currently issued to the signed-in user.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(CurrentUserId(), cancellationToken);
        return NoContent();
    }

    private int CurrentUserId() =>
        int.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub),
            NumberStyles.None, CultureInfo.InvariantCulture, out var userId) && userId > 0
            ? userId
            : throw new AppUnauthorizedException("The token identity is invalid.");
}
