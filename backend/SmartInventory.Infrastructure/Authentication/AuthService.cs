using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Common;
using SmartInventory.Application.Entities;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Authentication;

public sealed class AuthService(
    AppDbContext db,
    IPasswordHasher<User> passwordHasher,
    IAccessTokenIssuer tokenIssuer) : IAuthService
{
    private static readonly User DummyUser = new();
    private static readonly string DummyPasswordHash =
        new PasswordHasher<User>().HashPassword(DummyUser, "timing-comparison-only");

    public async Task<LoginResponse> LoginAsync(LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var username = request.Username?.Trim();
        var user = await db.Users.SingleOrDefaultAsync(
            item => item.Username == username, cancellationToken);
        var password = request.Password ?? string.Empty;
        var verification = VerifyPassword(user ?? DummyUser,
            user?.PasswordHash ?? DummyPasswordHash, password);

        if (user is null || !user.IsActive || !UserRoles.IsValid(user.Role) ||
            verification == PasswordVerificationResult.Failed)
        {
            throw new AppUnauthorizedException("Username or password is incorrect, or the account is inactive.");
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            user.UpdatedAt = DateTime.UtcNow;
            await SaveChangesAsync(cancellationToken);
        }

        var token = tokenIssuer.Issue(user);
        return new LoginResponse(token.Value, token.ExpiresAt, UserProfile.FromUser(user));
    }

    public async Task<UserProfile> GetProfileAsync(int userId,
        CancellationToken cancellationToken = default) =>
        UserProfile.FromUser(await GetActiveUserAsync(userId, cancellationToken));

    public async Task<UserProfile> UpdateProfileAsync(int userId, UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email?.Trim() ?? string.Empty;
        var fullName = request.FullName?.Trim() ?? string.Empty;
        var errors = new Dictionary<string, string[]>();
        if (email.Length is 0 or > 254 || !new EmailAddressAttribute().IsValid(email))
            errors["email"] = ["Enter a valid email address of at most 254 characters."];
        if (fullName.Length is 0 or > 160)
            errors["fullName"] = ["Full name must contain 1 to 160 characters."];
        if (errors.Count != 0)
            throw new AppValidationException("Profile validation failed.", errors);

        var user = await GetActiveUserAsync(userId, cancellationToken);
        user.Email = email;
        user.FullName = fullName;
        user.UpdatedAt = DateTime.UtcNow;
        await SaveChangesAsync(cancellationToken);
        return UserProfile.FromUser(user);
    }

    public async Task ChangePasswordAsync(int userId, ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length is < 12 or > 128)
            throw new AppValidationException("Password validation failed.",
                new Dictionary<string, string[]> { ["newPassword"] = ["Use 12 to 128 characters."] });

        var user = await GetActiveUserAsync(userId, cancellationToken);
        if (VerifyPassword(user, user.PasswordHash, request.CurrentPassword ?? string.Empty) ==
            PasswordVerificationResult.Failed)
        {
            throw new AppUnauthorizedException("The current password is incorrect.");
        }

        if (request.NewPassword == request.CurrentPassword)
            throw new AppValidationException("Password validation failed.",
                new Dictionary<string, string[]> { ["newPassword"] = ["Choose a different password."] });

        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        user.TokenVersion = checked(user.TokenVersion + 1);
        user.UpdatedAt = DateTime.UtcNow;
        await SaveChangesAsync(cancellationToken);
    }

    public async Task LogoutAsync(int userId, CancellationToken cancellationToken = default)
    {
        var count = await db.Users.Where(user => user.Id == userId && user.IsActive)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.TokenVersion, user => user.TokenVersion + 1)
                .SetProperty(user => user.UpdatedAt, DateTime.UtcNow), cancellationToken);
        if (count == 0)
            throw new AppUnauthorizedException("The account is no longer available.");
    }

    private async Task<User> GetActiveUserAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (user is null || !user.IsActive || !UserRoles.IsValid(user.Role))
            throw new AppUnauthorizedException("The account is no longer available.");
        return user;
    }

    private PasswordVerificationResult VerifyPassword(User user, string hash, string password)
    {
        try
        {
            return passwordHasher.VerifyHashedPassword(user, hash, password);
        }
        catch (FormatException)
        {
            return PasswordVerificationResult.Failed;
        }
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AppConflictException("The account changed during this request. Sign in again and retry.");
        }
    }
}
