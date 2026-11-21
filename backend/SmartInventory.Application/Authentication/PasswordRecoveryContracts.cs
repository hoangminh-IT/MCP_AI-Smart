using System.ComponentModel.DataAnnotations;

namespace SmartInventory.Application.Authentication;

public sealed record ForgotPasswordRequest(
    [Required, MaxLength(80)] string Username,
    [Required, EmailAddress, MaxLength(254)] string Email);

public sealed record ResetForgottenPasswordRequest(
    [Required, StringLength(64, MinimumLength = 64)] string Token,
    [Required] string NewPassword,
    [Required] string ConfirmPassword);

public sealed record PasswordRecoveryResponse(string Message);

public interface IPasswordRecoveryService
{
    Task RequestAsync(ForgotPasswordRequest request, CancellationToken cancellationToken);
    Task ResetAsync(ResetForgottenPasswordRequest request, CancellationToken cancellationToken);
}

public interface IPasswordRecoveryEmailSender
{
    bool IsConfigured { get; }
    Uri CreateResetLink(string token);
    Task SendAsync(string email, string fullName, Uri resetLink, CancellationToken cancellationToken);
}

public sealed class PasswordRecoveryUnavailableException() : Exception(
    "Chức năng gửi email khôi phục chưa sẵn sàng. Vui lòng liên hệ quản trị viên.");
