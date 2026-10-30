using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Application.Common;
using SmartInventory.Application.Settings;

namespace SmartInventory.Api.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize(Roles = "Admin,Staff")]
public sealed class SettingsController(ISettingsService settings) : ControllerBase
{
    [HttpGet]
    public Task<SettingsDto> Get(CancellationToken cancellationToken) => settings.GetSettingsAsync(CurrentUserId(), cancellationToken);

    [HttpPut]
    [Authorize(Roles = "Admin")]
    public Task<SettingsDto> Update(SettingsRequest request, CancellationToken cancellationToken) =>
        settings.UpdateSettingsAsync(CurrentUserId(), request, cancellationToken);

    [HttpGet("/api/preferences/me")]
    public Task<PreferencesDto> Preferences(CancellationToken cancellationToken) => settings.GetPreferencesAsync(CurrentUserId(), cancellationToken);

    [HttpPut("/api/preferences/me")]
    public Task<PreferencesDto> UpdatePreferences(PreferencesRequest request, CancellationToken cancellationToken) =>
        settings.UpdatePreferencesAsync(CurrentUserId(), request, cancellationToken);

    private int CurrentUserId() => int.TryParse(User.FindFirstValue("sub"), NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
        ? id : throw new AppUnauthorizedException("The token identity is invalid.");
}
