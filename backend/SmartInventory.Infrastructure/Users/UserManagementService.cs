using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;
using SmartInventory.Application.Entities;
using SmartInventory.Application.Users;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Users;

public sealed class UserManagementService(AppDbContext database, IPasswordHasher<User> passwordHasher) : IUserManagementService
{
    // Transaction-scoped, database-wide namespace for administrator-account mutations.
    private const int LockNamespace = 1397313110;
    private const int LockKey = 13;

    public async Task<PagedResult<ManagedUserDto>> GetUsersAsync(int actorId, UserManagementQuery query,
        CancellationToken cancellationToken)
    {
        await RequireAdminAsync(actorId, cancellationToken);
        UserManagementRules.ValidateQuery(query);
        var users = database.Users.AsNoTracking().AsQueryable();
        if (query.Role is not null) users = users.Where(user => user.Role == query.Role);
        if (query.IsActive.HasValue) users = users.Where(user => user.IsActive == query.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = "%" + query.Search.Trim() + "%";
            users = users.Where(user => EF.Functions.ILike(user.Username, pattern) ||
                EF.Functions.ILike(user.FullName, pattern) || EF.Functions.ILike(user.Email, pattern));
        }
        var sorted = (query.SortBy?.Trim().ToLowerInvariant() ?? "username") switch
        {
            "username" => query.Descending ? users.OrderByDescending(user => user.Username) : users.OrderBy(user => user.Username),
            "fullname" => query.Descending ? users.OrderByDescending(user => user.FullName) : users.OrderBy(user => user.FullName),
            "email" => query.Descending ? users.OrderByDescending(user => user.Email) : users.OrderBy(user => user.Email),
            "role" => query.Descending ? users.OrderByDescending(user => user.Role) : users.OrderBy(user => user.Role),
            "isactive" => query.Descending ? users.OrderByDescending(user => user.IsActive) : users.OrderBy(user => user.IsActive),
            "createdat" => query.Descending ? users.OrderByDescending(user => user.CreatedAt) : users.OrderBy(user => user.CreatedAt),
            "updatedat" => query.Descending ? users.OrderByDescending(user => user.UpdatedAt) : users.OrderBy(user => user.UpdatedAt),
            _ => throw new AppValidationException("Unsupported sortBy. Use username, fullName, email, role, isActive, createdAt or updatedAt.")
        };
        var count = await users.CountAsync(cancellationToken);
        var items = await Project(sorted.ThenBy(user => user.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize))
            .ToListAsync(cancellationToken);
        return new(items, count, query.Page, query.PageSize);
    }

    public async Task<ManagedUserDto> GetUserAsync(int actorId, int id, CancellationToken cancellationToken)
    {
        await RequireAdminAsync(actorId, cancellationToken);
        return await Project(database.Users.AsNoTracking().Where(user => user.Id == id)).SingleOrDefaultAsync(cancellationToken)
            ?? throw new AppNotFoundException("User was not found.");
    }

    public async Task<ManagedUserDto> CreateUserAsync(int actorId, CreateUserRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await LockAdministrationAsync(cancellationToken);
        await RequireAdminAsync(actorId, cancellationToken);
        UserManagementRules.NormalizeAndValidate(request);
        if (await database.Users.AnyAsync(user => user.Username == request.Username, cancellationToken))
            throw new AppConflictException("A user with this username already exists.");
        var now = DateTime.UtcNow;
        var user = new User { Username = request.Username, CreatedAt = now, UpdatedAt = now };
        Apply(user, request);
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        database.Users.Add(user);
        await SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ManagedUserDto.FromUser(user);
    }

    public async Task<ManagedUserDto> UpdateUserAsync(int actorId, int id, UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await LockAdministrationAsync(cancellationToken);
        await RequireAdminAsync(actorId, cancellationToken);
        UserManagementRules.NormalizeAndValidate(request);
        var user = await GetLockedUserAsync(id, cancellationToken);
        var credentialsChanged = user.Role != request.Role || user.IsActive != request.IsActive;
        if (user.IsActive && user.Role == UserRoles.Admin && (!request.IsActive || request.Role != UserRoles.Admin) &&
            !await database.Users.AnyAsync(other => other.Id != id && other.IsActive && other.Role == UserRoles.Admin, cancellationToken))
            throw new AppConflictException("The last active administrator cannot be deactivated or assigned another role.");
        Apply(user, request);
        if (credentialsChanged)
        {
            user.TokenVersion = checked(user.TokenVersion + 1);
            await RevokeSessionsAsync(user.Id, user.UpdatedAt, cancellationToken);
        }
        await SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ManagedUserDto.FromUser(user);
    }

    public async Task ResetPasswordAsync(int actorId, int id, ResetUserPasswordRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await LockAdministrationAsync(cancellationToken);
        await RequireAdminAsync(actorId, cancellationToken);
        UserManagementRules.ValidatePasswordReset(request);
        var user = await GetLockedUserAsync(id, cancellationToken);
        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        user.TokenVersion = checked(user.TokenVersion + 1);
        user.UpdatedAt = DateTime.UtcNow;
        await RevokeSessionsAsync(user.Id, user.UpdatedAt, cancellationToken);
        await SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private Task LockAdministrationAsync(CancellationToken cancellationToken) => database.Database.ExecuteSqlInterpolatedAsync(
        $"SELECT pg_advisory_xact_lock({LockNamespace}, {LockKey})", cancellationToken);

    private Task<int> RevokeSessionsAsync(int userId, DateTime now, CancellationToken cancellationToken) =>
        database.RefreshSessions.Where(session => session.UserId == userId && session.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAt, now), cancellationToken);

    private async Task RequireAdminAsync(int actorId, CancellationToken cancellationToken)
    {
        if (!await database.Users.AsNoTracking().AnyAsync(user => user.Id == actorId && user.IsActive &&
            user.Role == UserRoles.Admin, cancellationToken))
            throw new AppForbiddenException("An active administrator account is required.");
    }

    private async Task<User> GetLockedUserAsync(int id, CancellationToken cancellationToken)
    {
        var user = await database.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) ?? throw new AppNotFoundException("User was not found.");
        // A reused scope may already track this row; refresh while holding the row lock.
        await database.Entry(user).ReloadAsync(cancellationToken);
        return user;
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            throw new AppConflictException("The account changed during this request. Reload it and retry.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_users_username" })
        {
            throw new AppConflictException("A user with this username already exists.");
        }
    }

    private static IQueryable<ManagedUserDto> Project(IQueryable<User> users) => users.Select(user =>
        new ManagedUserDto(user.Id, user.Username, user.Email, user.FullName, user.Role,
            user.IsActive, user.CreatedAt, user.UpdatedAt));

    private static void Apply(User user, UpdateUserRequest request)
    {
        user.Email = request.Email;
        user.FullName = request.FullName;
        user.Role = request.Role;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
    }
}
