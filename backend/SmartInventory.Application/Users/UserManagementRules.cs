using System.ComponentModel.DataAnnotations;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Common;

namespace SmartInventory.Application.Users;

public static class UserManagementRules
{
    public static void ValidateQuery(UserManagementQuery query)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue)
            throw new AppValidationException("page must be positive and pageSize must be between 1 and 100.");
        if (query.Search?.Length > 200) throw new AppValidationException("search must contain at most 200 characters.");
        if (query.Role is not null && !UserRoles.IsValid(query.Role))
            throw new AppValidationException("role must be Admin, Staff or Customer when supplied.");
    }

    public static void NormalizeAndValidate(UpdateUserRequest request)
    {
        request.Email = request.Email?.Trim() ?? string.Empty;
        request.FullName = request.FullName?.Trim() ?? string.Empty;
        if (request is CreateUserRequest creation) creation.Username = creation.Username?.Trim() ?? string.Empty;
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, true);
        var errors = results.SelectMany(result => result.MemberNames.DefaultIfEmpty(string.Empty)
                .Select(member => (member, message: result.ErrorMessage ?? "The submitted value is invalid.")))
            .GroupBy(item => item.member)
            .ToDictionary(group => CamelCase(group.Key), group => group.Select(item => item.message).ToArray());
        if (request.FullName.Length == 0) errors["fullName"] = ["Full name must contain 1 to 160 characters."];
        if (!UserRoles.IsValid(request.Role)) errors["role"] = ["Role must be Admin, Staff or Customer."];
        if (request is CreateUserRequest create && (create.Username.Length is < 1 or > 80 ||
            create.Username.Any(character => !IsUsernameCharacter(character))))
            errors["username"] = ["Username must contain 1 to 80 ASCII letters, digits, dots, underscores or hyphens."];
        if (errors.Count > 0) throw new AppValidationException("User validation failed.", errors);
    }

    public static void ValidatePasswordReset(ResetUserPasswordRequest request)
    {
        if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length is < 12 or > 128)
            throw new AppValidationException("Password validation failed.",
                new Dictionary<string, string[]> { ["newPassword"] = ["Use 12 to 128 characters."] });
    }

    private static bool IsUsernameCharacter(char character) =>
        character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-';

    private static string CamelCase(string value) => value.Length == 0 ? value : char.ToLowerInvariant(value[0]) + value[1..];
}
