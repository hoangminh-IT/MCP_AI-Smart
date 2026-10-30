using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Common;
using SmartInventory.Application.Entities;
using SmartInventory.Application.Settings;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Settings;

public sealed class SettingsService(AppDbContext database) : ISettingsService
{
    public async Task<SettingsDto> GetSettingsAsync(int actorId, CancellationToken cancellationToken)
    {
        await RequireUserAsync(actorId, false, cancellationToken);
        return ToDto(await database.ApplicationSettings.AsNoTracking().SingleAsync(item => item.Id == 1, cancellationToken));
    }

    public async Task<SettingsDto> UpdateSettingsAsync(int actorId, SettingsRequest request, CancellationToken cancellationToken)
    {
        await RequireUserAsync(actorId, true, cancellationToken);
        request.CompanyName = request.CompanyName?.Trim() ?? string.Empty;
        request.ContactEmail = Optional(request.ContactEmail);
        request.Phone = Optional(request.Phone);
        request.Address = Optional(request.Address);
        Validate(request);
        if (request.ContactEmail is not null && !new EmailAddressAttribute().IsValid(request.ContactEmail))
            throw new AppValidationException("Settings validation failed.",
                new Dictionary<string, string[]> { ["contactEmail"] = ["Enter a valid contact email address."] });
        var settings = await database.ApplicationSettings.SingleAsync(item => item.Id == 1, cancellationToken);
        if (settings.Revision != request.Revision) throw Conflict();
        settings.CompanyName = request.CompanyName;
        settings.ContactEmail = request.ContactEmail;
        settings.Phone = request.Phone;
        settings.Address = request.Address;
        settings.DefaultMinStock = request.DefaultMinStock;
        settings.ReportDefaultDays = request.ReportDefaultDays;
        settings.Revision = checked(settings.Revision + 1);
        settings.UpdatedAt = DateTime.UtcNow;
        await SaveAsync(cancellationToken);
        return ToDto(settings);
    }

    public async Task<PreferencesDto> GetPreferencesAsync(int actorId, CancellationToken cancellationToken)
    {
        await RequireUserAsync(actorId, false, cancellationToken);
        var preferences = await database.UserPreferences.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == actorId, cancellationToken);
        return preferences is null ? new(10, 0, null) : ToDto(preferences);
    }

    public async Task<PreferencesDto> UpdatePreferencesAsync(int actorId, PreferencesRequest request, CancellationToken cancellationToken)
    {
        await RequireUserAsync(actorId, false, cancellationToken);
        Validate(request);
        if (request.DefaultPageSize is not (10 or 25 or 50))
            throw new AppValidationException("defaultPageSize must be 10, 25 or 50.");
        var preferences = await database.UserPreferences.SingleOrDefaultAsync(item => item.UserId == actorId, cancellationToken);
        if (preferences is null)
        {
            if (request.Revision != 0) throw Conflict();
            preferences = new UserPreferences { UserId = actorId };
            database.UserPreferences.Add(preferences);
        }
        else
        {
            if (request.Revision != preferences.Revision) throw Conflict();
            preferences.Revision = checked(preferences.Revision + 1);
        }
        preferences.DefaultPageSize = request.DefaultPageSize;
        preferences.UpdatedAt = DateTime.UtcNow;
        await SaveAsync(cancellationToken);
        return ToDto(preferences);
    }

    private async Task RequireUserAsync(int actorId, bool adminOnly, CancellationToken cancellationToken)
    {
        var user = await database.Users.AsNoTracking().Where(item => item.Id == actorId && item.IsActive)
            .Select(item => new { item.Role }).SingleOrDefaultAsync(cancellationToken);
        if (user is null || !UserRoles.CanManageInventory(user.Role) || adminOnly && user.Role != UserRoles.Admin)
            throw new AppForbiddenException(adminOnly ? "An active administrator account is required." : "An active user account is required.");
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw Conflict(); }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException
            { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation }) { throw Conflict(); }
    }

    private static void Validate(object request)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), results, true))
            throw new AppValidationException("Settings validation failed.");
    }

    private static AppConflictException Conflict() => new("Settings or preferences changed. Reload them and retry with the current revision.");
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static SettingsDto ToDto(ApplicationSettings value) => new(value.CompanyName, value.ContactEmail, value.Phone,
        value.Address, value.DefaultMinStock, value.ReportDefaultDays, value.Revision, value.UpdatedAt);
    private static PreferencesDto ToDto(UserPreferences value) => new(value.DefaultPageSize, value.Revision, value.UpdatedAt);
}
