using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartInventory.Application.Authentication;

namespace SmartInventory.Api.Controllers;

public sealed partial class AuthController
{
    [HttpPost("forgot-password"), AllowAnonymous, EnableRateLimiting("recovery")]
    public async Task<PasswordRecoveryResponse> ForgotPassword(ForgotPasswordRequest request,
        [FromServices] IPasswordRecoveryService recovery, CancellationToken cancellationToken)
    {
        await recovery.RequestAsync(request, cancellationToken);
        return new("Nếu tên đăng nhập và email khớp với một tài khoản đang hoạt động, bạn sẽ nhận được liên kết đặt lại mật khẩu. Vui lòng kiểm tra cả thư rác.");
    }

    [HttpPost("reset-password"), AllowAnonymous, EnableRateLimiting("recovery")]
    public async Task<IActionResult> ResetForgottenPassword(ResetForgottenPasswordRequest request,
        [FromServices] IPasswordRecoveryService recovery, CancellationToken cancellationToken)
    {
        await recovery.ResetAsync(request, cancellationToken);
        return NoContent();
    }
}
