using System.ComponentModel.DataAnnotations;

namespace SmartInventory.Application.Settings;

public sealed record SettingsDto(string CompanyName, string? ContactEmail, string? Phone, string? Address,
    int DefaultMinStock, int ReportDefaultDays, int Revision, DateTime UpdatedAt);

public sealed class SettingsRequest
{
    [Required, MaxLength(200)] public string CompanyName { get; set; } = string.Empty;
    [MaxLength(254)] public string? ContactEmail { get; set; }
    [MaxLength(40)] public string? Phone { get; set; }
    [MaxLength(1000)] public string? Address { get; set; }
    [Range(0, int.MaxValue)] public int DefaultMinStock { get; set; }
    [Range(1, 366)] public int ReportDefaultDays { get; set; } = 30;
    [Range(1, int.MaxValue)] public int Revision { get; set; }
}

public sealed record PreferencesDto(int DefaultPageSize, int Revision, DateTime? UpdatedAt);

public sealed class PreferencesRequest
{
    public int DefaultPageSize { get; set; } = 10;
    [Range(0, int.MaxValue)] public int Revision { get; set; }
}

public interface ISettingsService
{
    Task<SettingsDto> GetSettingsAsync(int actorId, CancellationToken cancellationToken);
    Task<SettingsDto> UpdateSettingsAsync(int actorId, SettingsRequest request, CancellationToken cancellationToken);
    Task<PreferencesDto> GetPreferencesAsync(int actorId, CancellationToken cancellationToken);
    Task<PreferencesDto> UpdatePreferencesAsync(int actorId, PreferencesRequest request, CancellationToken cancellationToken);
}
