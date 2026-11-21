using System.Globalization;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Common;
using SmartInventory.Application.Entities;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Authentication;

public sealed class PasswordRecoveryService(AppDbContext database, IPasswordHasher<User> hasher,
    IPasswordRecoveryEmailSender sender, ILogger<PasswordRecoveryService> logger) : IPasswordRecoveryService
{
    public async Task RequestAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!sender.IsConfigured) throw new PasswordRecoveryUnavailableException();
        var username = request.Username.Trim();
        var email = request.Email.Trim();
        var candidate = await database.Users.AsNoTracking().SingleOrDefaultAsync(user =>
            user.Username == username, cancellationToken);
        if (candidate is null) return;
        string rawToken;
        string fullName;
        await using (var transaction = await database.Database.BeginTransactionAsync(cancellationToken))
        {
            var user = await LockedUser(candidate.Id, cancellationToken);
            if (!user.IsActive || !UserRoles.IsValid(user.Role) || !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)) return;
            var now = DateTime.UtcNow;
            await database.PasswordResetTokens.Where(token => token.UserId == user.Id && token.ConsumedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.ConsumedAt, now), cancellationToken);
            rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            database.PasswordResetTokens.Add(new PasswordResetToken
            {
                Id = Guid.NewGuid(), UserId = user.Id, TokenHash = Hash(rawToken), TokenVersion = user.TokenVersion,
                CreatedAt = now, ExpiresAt = now.AddMinutes(15)
            });
            Audit(user.Id, "PasswordRecoveryRequested", now);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            email = user.Email;
            fullName = user.FullName;
        }
        try { await sender.SendAsync(email, fullName, sender.CreateResetLink(rawToken), cancellationToken); }
        catch (Exception error) when (error is SmtpException or IOException)
        {
            // Keep the public response identical; never log the address, link, token or transport exception details.
            logger.LogError("Password recovery delivery failed ({ExceptionType}).", error.GetType().Name);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError("Password recovery delivery timed out.");
        }
    }

    public async Task ResetAsync(ResetForgottenPasswordRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword != request.ConfirmPassword)
            throw new AppValidationException("Nhập mật khẩu mới và xác nhận mật khẩu trùng khớp.");
        if (request.Token.Length != 64 || request.Token.Any(character => !char.IsAsciiHexDigit(character))) throw InvalidLink();
        var hash = Hash(request.Token);
        var candidate = await database.PasswordResetTokens.AsNoTracking().SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken)
            ?? throw InvalidLink();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockedUser(candidate.UserId, cancellationToken);
        var token = await database.PasswordResetTokens.FromSqlInterpolated(
            $"SELECT * FROM password_reset_tokens WHERE id = {candidate.Id} FOR UPDATE").SingleAsync(cancellationToken);
        await database.Entry(token).ReloadAsync(cancellationToken);
        var now = DateTime.UtcNow;
        if (token.ConsumedAt.HasValue || token.ExpiresAt <= now || token.TokenVersion != user.TokenVersion ||
            !user.IsActive || !UserRoles.IsValid(user.Role)) throw InvalidLink();
        user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
        user.TokenVersion = checked(user.TokenVersion + 1);
        user.UpdatedAt = now;
        token.ConsumedAt = now;
        await database.RefreshSessions.Where(session => session.UserId == user.Id && session.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAt, now), cancellationToken);
        await database.PasswordResetTokens.Where(item => item.UserId == user.Id && item.Id != token.Id && item.ConsumedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ConsumedAt, now), cancellationToken);
        Audit(user.Id, "PasswordRecovered", now);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<User> LockedUser(int id, CancellationToken cancellationToken)
    {
        var user = await database.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {id} FOR UPDATE").SingleAsync(cancellationToken);
        await database.Entry(user).ReloadAsync(cancellationToken);
        return user;
    }

    private void Audit(int id, string action, DateTime now) => database.AuditLogs.Add(new AuditLog
    {
        Action = action, EntityName = nameof(User), TargetId = id.ToString(CultureInfo.InvariantCulture),
        Status = "Succeeded", CreatedAt = now
    });
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static AppValidationException InvalidLink() => new("Liên kết đặt lại mật khẩu không hợp lệ, đã được dùng hoặc đã hết hạn. Vui lòng yêu cầu liên kết mới.");
}
