using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SmartInventory.Application.Authentication;

namespace SmartInventory.Infrastructure.Authentication;

public sealed class PasswordRecoveryOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Smart Inventory";
    public string PublicBaseUrl { get; set; } = string.Empty;
}

public sealed class SmtpPasswordRecoveryEmailSender(IOptions<PasswordRecoveryOptions> configuration,
    IHostEnvironment environment) : IPasswordRecoveryEmailSender
{
    private PasswordRecoveryOptions Settings => configuration.Value;
    private bool LocalTest => (environment.IsDevelopment() || environment.IsEnvironment("Testing")) &&
        Settings.Host is "127.0.0.1" or "localhost" or "::1";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Settings.Host) && Settings.Port is > 0 and <= 65535 &&
        MailAddress.TryCreate(Settings.FromEmail, out _) &&
        (LocalTest || Settings.EnableSsl && !string.IsNullOrWhiteSpace(Settings.Username) && !string.IsNullOrWhiteSpace(Settings.Password)) &&
        Uri.TryCreate(Settings.PublicBaseUrl, UriKind.Absolute, out var origin) &&
        string.IsNullOrEmpty(origin.UserInfo) && string.IsNullOrEmpty(origin.Query) && string.IsNullOrEmpty(origin.Fragment) &&
        (origin.Scheme == "https" || origin.Scheme == "http" && origin.IsLoopback &&
            (environment.IsDevelopment() || environment.IsEnvironment("Testing")));

    public Uri CreateResetLink(string token)
    {
        if (!IsConfigured) throw new PasswordRecoveryUnavailableException();
        // A fragment is not sent in HTTP requests or referrer headers.
        return new Uri(new Uri(Settings.PublicBaseUrl.TrimEnd('/') + "/"), "reset-password#token=" + Uri.EscapeDataString(token));
    }

    public async Task SendAsync(string email, string fullName, Uri resetLink, CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new PasswordRecoveryUnavailableException();
        using var message = new MailMessage
        {
            From = new MailAddress(Settings.FromEmail, Settings.FromName),
            Subject = "Đặt lại mật khẩu Smart Inventory",
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false,
            Body = $"Xin chào {fullName},\n\nBạn vừa yêu cầu đặt lại mật khẩu Smart Inventory.\n" +
                $"Mở liên kết sau để chọn mật khẩu mới (hiệu lực 15 phút, chỉ dùng một lần):\n\n{resetLink.AbsoluteUri}\n\n" +
                "Nếu bạn không yêu cầu thay đổi này, hãy bỏ qua email. Mật khẩu của bạn vẫn giữ nguyên.\n\nSmart Inventory"
        };
        message.To.Add(new MailAddress(email));
        using var smtp = new SmtpClient(Settings.Host, Settings.Port)
        {
            EnableSsl = Settings.EnableSsl,
            UseDefaultCredentials = false,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
        if (!string.IsNullOrWhiteSpace(Settings.Username)) smtp.Credentials = new NetworkCredential(Settings.Username, Settings.Password);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        await smtp.SendMailAsync(message, timeout.Token);
    }
}
