using System.ComponentModel.DataAnnotations;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Entities;

namespace SmartInventory.Application.Users;

public sealed class UserManagementQuery
{
    public string? Search { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string SortBy { get; set; } = "username";
    public bool Descending { get; set; }
}

public class UpdateUserRequest
{
    [Required, EmailAddress, MaxLength(254)] public string Email { get; set; } = string.Empty;
    [Required, MaxLength(160)] public string FullName { get; set; } = string.Empty;
    [Required, RegularExpression("^(Admin|Staff|Customer)$")] public string Role { get; set; } = UserRoles.Staff;
    public bool IsActive { get; set; } = true;
}

public sealed class CreateUserRequest : UpdateUserRequest
{
    [Required, MaxLength(80)] public string Username { get; set; } = string.Empty;
    [Required, MinLength(12), MaxLength(128)] public string Password { get; set; } = string.Empty;
}

public sealed class ResetUserPasswordRequest
{
    [Required, MinLength(12), MaxLength(128)] public string NewPassword { get; set; } = string.Empty;
}

/// <summary>Administrative account metadata. Credential hashes and token versions are deliberately excluded.</summary>
public sealed record ManagedUserDto(int Id, string Username, string Email, string FullName, string Role,
    bool IsActive, DateTime CreatedAt, DateTime UpdatedAt)
{
    public static ManagedUserDto FromUser(User user) => new(user.Id, user.Username, user.Email, user.FullName,
        user.Role, user.IsActive, user.CreatedAt, user.UpdatedAt);
}

public interface IUserManagementService
{
    Task<PagedResult<ManagedUserDto>> GetUsersAsync(int actorId, UserManagementQuery query, CancellationToken cancellationToken);
    Task<ManagedUserDto> GetUserAsync(int actorId, int id, CancellationToken cancellationToken);
    Task<ManagedUserDto> CreateUserAsync(int actorId, CreateUserRequest request, CancellationToken cancellationToken);
    Task<ManagedUserDto> UpdateUserAsync(int actorId, int id, UpdateUserRequest request, CancellationToken cancellationToken);
    Task ResetPasswordAsync(int actorId, int id, ResetUserPasswordRequest request, CancellationToken cancellationToken);
}
