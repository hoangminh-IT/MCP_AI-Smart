using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;
using SmartInventory.Application.Users;

namespace SmartInventory.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public sealed class UsersController(IUserManagementService users) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ManagedUserDto>> List([FromQuery] UserManagementQuery query, CancellationToken cancellationToken) =>
        users.GetUsersAsync(CurrentUserId(), query, cancellationToken);

    [HttpGet("{id:int}")]
    public Task<ManagedUserDto> Get(int id, CancellationToken cancellationToken) =>
        users.GetUserAsync(CurrentUserId(), id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<ManagedUserDto>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await users.CreateUserAsync(CurrentUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
    }

    [HttpPut("{id:int}")]
    public Task<ManagedUserDto> Update(int id, UpdateUserRequest request, CancellationToken cancellationToken) =>
        users.UpdateUserAsync(CurrentUserId(), id, request, cancellationToken);

    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, ResetUserPasswordRequest request, CancellationToken cancellationToken)
    {
        await users.ResetPasswordAsync(CurrentUserId(), id, request, cancellationToken);
        return NoContent();
    }

    private int CurrentUserId() => int.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub),
        NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
        ? id : throw new AppUnauthorizedException("The token identity is invalid.");
}
