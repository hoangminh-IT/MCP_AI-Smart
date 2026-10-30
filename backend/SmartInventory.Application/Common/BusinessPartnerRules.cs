using System.ComponentModel.DataAnnotations;

namespace SmartInventory.Application.Common;

/// <summary>Shared contact and list validation for suppliers and customers.</summary>
public static class BusinessPartnerRules
{
    public static string NormalizeCode(string? value) => value?.Trim().ToUpperInvariant() ?? string.Empty;
    public static string NormalizeName(string? value) => value?.Trim() ?? string.Empty;
    public static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static void ValidateQuery(int page, int pageSize, string? search)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new AppValidationException("page must be positive and pageSize must be between 1 and 100.");
        if (search?.Length > 200) throw new AppValidationException("Search must be at most 200 characters.");
    }

    public static void ValidateRequest(object request, string? email, string detail)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, true);
        var errors = results.SelectMany(result => result.MemberNames.DefaultIfEmpty("request")
                .Select(member => new { Member = char.ToLowerInvariant(member[0]) + member[1..], Message = result.ErrorMessage ?? "Value is invalid." }))
            .GroupBy(result => result.Member).ToDictionary(group => group.Key, group => group.Select(result => result.Message).ToArray());
        if (email is not null && !new EmailAddressAttribute().IsValid(email)) errors["email"] = ["Enter a valid email address."];
        if (errors.Count > 0) throw new AppValidationException(detail, errors);
    }
}
