using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Common;
using SmartInventory.Application.Entities;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Authentication;

/// <summary>Explicit local administration command; never exposed as an HTTP endpoint.</summary>
public sealed class AdminBootstrapService(
    AppDbContext db,
    IPasswordHasher<User> passwordHasher,
    IConfiguration configuration) : IAdminBootstrapService
{
    public async Task<BootstrapAdminResult> BootstrapAsync(CancellationToken cancellationToken = default)
    {
        var username = configuration["Bootstrap:Username"]?.Trim() ?? string.Empty;
        var password = configuration["Bootstrap:Password"] ?? string.Empty;
        var email = configuration["Bootstrap:Email"]?.Trim() ?? string.Empty;
        var fullName = configuration["Bootstrap:FullName"]?.Trim() ?? username;

        var errors = new Dictionary<string, string[]>();
        if (!Regex.IsMatch(username, "^[A-Za-z0-9._-]{3,80}$", RegexOptions.CultureInvariant))
            errors["username"] = ["Bootstrap:Username must contain 3 to 80 letters, digits, dots, underscores or hyphens."];
        if (password.Length is < 12 or > 128)
            errors["password"] = ["Bootstrap:Password must contain 12 to 128 characters."];
        if (email.Length is 0 or > 254 || !new EmailAddressAttribute().IsValid(email))
            errors["email"] = ["Bootstrap:Email must be a valid email address."];
        if (fullName.Length is 0 or > 160)
            errors["fullName"] = ["Bootstrap:FullName must contain 1 to 160 characters."];
        if (errors.Count != 0)
            throw new AppValidationException("Provide bootstrap credentials through user secrets or environment variables.", errors);

        var existing = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(user => user.Username == username, cancellationToken);
        if (existing is not null)
        {
            if (existing.Role == UserRoles.Admin && existing.IsActive)
                return new BootstrapAdminResult(false, username);
            throw new AppConflictException("The bootstrap username already belongs to another account.");
        }

        if (await db.Users.AnyAsync(user => user.IsActive && user.Role == UserRoles.Admin, cancellationToken))
            throw new AppConflictException("An active administrator already exists. Bootstrap does not create additional administrators.");

        var user = new User
        {
            Username = username,
            Email = email,
            FullName = fullName,
            Role = UserRoles.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return new BootstrapAdminResult(true, username);
    }
}
