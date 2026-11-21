#:property PublishAot=false
// Run from the repository root: dotnet run --file scripts/configure-gmail.cs
using System.Diagnostics;
using System.Net.Mail;
using System.Text;
using System.Text.Json;

if (args is ["--help"] or ["-h"])
{
    Console.WriteLine("Configure Gmail SMTP privately: dotnet run --file scripts/configure-gmail.cs");
    Console.WriteLine("Requires a Gmail App Password. No email is sent by this command.");
    return 0;
}
if (Console.IsInputRedirected) { Console.Error.WriteLine("Use an interactive terminal."); return 1; }
var project = Path.Combine(Directory.GetCurrentDirectory(), "backend/SmartInventory.Api/SmartInventory.Api.csproj");
if (!File.Exists(project)) { Console.Error.WriteLine("Run from the AI-Smart-Inventory root directory."); return 1; }
Console.Write("Gmail sender address: ");
var email = Console.ReadLine()?.Trim() ?? "";
if (!MailAddress.TryCreate(email, out var mailbox) || mailbox.Address != email)
{
    Console.Error.WriteLine("Enter a valid sender address."); return 1;
}
Console.Write("Gmail App Password (hidden): ");
var buffer = new StringBuilder();
while (true)
{
    var key = Console.ReadKey(intercept: true);
    if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); break; }
    if (key.Key == ConsoleKey.Backspace) { if (buffer.Length > 0) buffer.Length--; }
    else if (!char.IsControl(key.KeyChar)) buffer.Append(key.KeyChar);
}
var password = string.Concat(buffer.ToString().Where(character => !char.IsWhiteSpace(character)));
buffer.Clear();
if (password.Length != 16) { Console.Error.WriteLine("Use the 16-character Gmail App Password, not your Gmail login password."); return 1; }
Console.Write("Frontend URL [http://127.0.0.1:5173]: ");
var publicUrl = Console.ReadLine()?.Trim();
if (string.IsNullOrEmpty(publicUrl)) publicUrl = "http://127.0.0.1:5173";
if (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var url) ||
    !(url.Scheme == "https" || url.Scheme == "http" && url.IsLoopback) ||
    url.UserInfo.Length > 0 || url.Query.Length > 0 || url.Fragment.Length > 0)
{
    Console.Error.WriteLine("Use HTTPS, or a local HTTP URL for development."); return 1;
}
var values = new Dictionary<string, string>
{
    ["PasswordRecovery:Host"] = "smtp.gmail.com", ["PasswordRecovery:Port"] = "587",
    ["PasswordRecovery:EnableSsl"] = "true", ["PasswordRecovery:Username"] = email,
    ["PasswordRecovery:Password"] = password, ["PasswordRecovery:FromEmail"] = email,
    ["PasswordRecovery:FromName"] = "Smart Inventory", ["PasswordRecovery:PublicBaseUrl"] = publicUrl
};
password = "";
var info = new ProcessStartInfo("dotnet") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
foreach (var argument in new[] { "user-secrets", "set", "--project", project }) info.ArgumentList.Add(argument);
using var process = Process.Start(info)!;
var output = process.StandardOutput.ReadToEndAsync();
var errors = process.StandardError.ReadToEndAsync();
await process.StandardInput.WriteAsync(JsonSerializer.Serialize(values));
process.StandardInput.Close(); values.Clear();
await process.WaitForExitAsync();
await Task.WhenAll(output, errors);
if (process.ExitCode != 0) { Console.Error.WriteLine("Could not save User Secrets. Exit code: " + process.ExitCode); return 1; }
Console.WriteLine("Gmail SMTP saved to API User Secrets. Restart AppHost to apply. No email was sent.");
return 0;
