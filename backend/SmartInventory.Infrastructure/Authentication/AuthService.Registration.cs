using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Common;
using SmartInventory.Application.Entities;

namespace SmartInventory.Infrastructure.Authentication;

public sealed partial class AuthService
{
    public async Task<UserProfile> RegisterAsync(RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        var email = request.Email?.Trim() ?? string.Empty;
        var fullName = request.FullName?.Trim() ?? string.Empty;
        ValidateRegistration(request, username, email, fullName);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (await db.Users.AnyAsync(user => user.Username == username, cancellationToken))
            throw DuplicateUsername();

        var now = DateTime.UtcNow;
        var user = new User
        {
            Username = username,
            Email = email,
            FullName = fullName,
            Role = UserRoles.Customer,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        try
        {
            // The database's normal Created audit uses the generated user ID. An explicit
            // registration event additionally records the new account as the actor.
            await SaveChangesAsync(cancellationToken);
            db.AuditLogs.Add(new AuditLog
            {
                ActorId = user.Id,
                Action = "Registered",
                EntityName = nameof(User),
                TargetId = user.Id.ToString(CultureInfo.InvariantCulture),
                Status = "Succeeded",
                CreatedAt = now
            });
            await SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_users_username" })
        {
            // Concurrent registrations may both pass the initial existence check.
            // The unique database index remains the authority; never expose provider details.
            throw DuplicateUsername();
        }

        // Registration creates no browser session. The user signs in separately.
        return UserProfile.FromUser(user);
    }

    private static void ValidateRegistration(RegisterRequest request, string username, string email, string fullName)
    {
        var errors = new Dictionary<string, string[]>();
        if (username.Length is < 1 or > 80 || username.Any(character =>
                character is not (>= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-')))
            errors["username"] = ["Username must contain 1 to 80 ASCII letters, digits, dots, underscores or hyphens."];
        if (email.Length is 0 or > 254 || !new EmailAddressAttribute().IsValid(email))
            errors["email"] = ["Enter a valid email address of at most 254 characters."];
        if (fullName.Length is 0 or > 160 || fullName.Any(char.IsControl))
            errors["fullName"] = ["Full name must contain 1 to 160 characters without control characters."];
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length is < 12 or > 128)
            errors["password"] = ["Use 12 to 128 characters."];
        if (string.IsNullOrEmpty(request.ConfirmPassword) || request.ConfirmPassword != request.Password)
            errors["confirmPassword"] = ["Password confirmation must match the password."];
        if (errors.Count != 0)
            throw new AppValidationException("Registration validation failed.", errors);
    }

    private static AppConflictException DuplicateUsername() =>
        new("A user with this username already exists.");
}
