using System.ComponentModel.DataAnnotations;
using SmartInventory.Application.Entities;

namespace SmartInventory.Application.Authentication;

public static class UserRoles
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";
    public static bool IsValid(string role) => role is Admin or Staff;
}

public sealed record LoginRequest(
    [Required, MaxLength(80)] string Username,
    [Required, MaxLength(128)] string Password);

public sealed record UpdateProfileRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MaxLength(160)] string FullName);

public sealed record ChangePasswordRequest(
    [Required, MaxLength(128)] string CurrentPassword,
    [Required, MinLength(12), MaxLength(128)] string NewPassword);

public sealed record UserProfile(int Id, string Username, string Email, string FullName, string Role)
{
    public static UserProfile FromUser(User user) =>
        new(user.Id, user.Username, user.Email, user.FullName, user.Role);
}

public sealed record AccessToken(string Value, DateTime ExpiresAt);
public sealed record LoginResponse(string AccessToken, DateTime ExpiresAt, UserProfile User);

public interface IAccessTokenIssuer
{
    AccessToken Issue(User user);
}

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<UserProfile> GetProfileAsync(int userId, CancellationToken cancellationToken = default);
    Task<UserProfile> UpdateProfileAsync(int userId, UpdateProfileRequest request,
        CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(int userId, ChangePasswordRequest request,
        CancellationToken cancellationToken = default);
    Task LogoutAsync(int userId, CancellationToken cancellationToken = default);
}

public sealed record BootstrapAdminResult(bool Created, string Username);

public interface IAdminBootstrapService
{
    Task<BootstrapAdminResult> BootstrapAsync(CancellationToken cancellationToken = default);
}
