using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Common;
using SmartInventory.Application.Entities;
using SmartInventory.Application.Users;
using SmartInventory.Infrastructure.Authentication;
using SmartInventory.Infrastructure.Data;
using SmartInventory.Infrastructure.Users;

internal static class PasswordRecoveryVerification
{
    public static async Task Run(Uri address, string connection, string oldCustomerToken, TestSmtpServer smtp, string outputPath)
    {
        using var http = new HttpClient { BaseAddress = address };
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options;
        var checks = new List<object>(); var passed = false;
        var hasher = new PasswordHasher<User>();
        try
        {
            var request = new { username = "verify_customer", email = "Customer@example.test" };
            var reply = await Call("RECOVERY-REQUEST", "/api/auth/forgot-password", request, 200);
            Check(reply!["token"] is null && reply["resetLink"] is null, "Public response contains no credential or reset link.");
            var rawToken = ExtractToken(await smtp.ReadMessage());
            await using var db = new AppDbContext(options);
            var target = await db.Users.AsNoTracking().SingleAsync(user => user.Username == "verify_customer");
            var stored = await db.PasswordResetTokens.AsNoTracking().SingleAsync(token => token.UserId == target.Id);
            Check(stored.TokenHash == Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))) && stored.TokenHash != rawToken,
                "Database stores only a hash of the recovery credential.");
            var unknown = await Call("RECOVERY-UNKNOWN", "/api/auth/forgot-password", new { username = "missing_user", email = "Customer@example.test" }, 200);
            Check(reply.ToJsonString() == unknown!.ToJsonString() && smtp.MessageCount == 1, "Unknown account returns the same message and sends no mail.");
            await Call("RECOVERY-RESET", "/api/auth/reset-password", new { token = rawToken, newPassword = "q", confirmPassword = "q" }, 204);
            await Call("RECOVERY-REPLAY", "/api/auth/reset-password", new { token = rawToken, newPassword = "z", confirmPassword = "z" }, 400);
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldCustomerToken);
            using (var oldSession = await http.GetAsync("/api/auth/me")) Check((int)oldSession.StatusCode == 401, "Old access token is revoked after recovery.");
            http.DefaultRequestHeaders.Authorization = null;
            using (var login = await http.PostAsJsonAsync("/api/auth/login", new { username = "verify_customer", password = "q" }))
                Check(login.IsSuccessStatusCode, "Recovered account logs in with the new short password.");
            var current = await db.Users.AsNoTracking().SingleAsync(user => user.Id == target.Id);
            Check(hasher.VerifyHashedPassword(current, current.PasswordHash, "q") != PasswordVerificationResult.Failed, "Recovered password is hashed.");

            var capture = new CapturingSender();
            var recovery = new PasswordRecoveryService(db, hasher, capture, NullLogger<PasswordRecoveryService>.Instance);
            await recovery.RequestAsync(new("verify_customer", "Customer@example.test"), default);
            var expiredToken = capture.Token;
            await db.PasswordResetTokens.Where(token => token.UserId == target.Id && token.ConsumedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.CreatedAt, DateTime.UtcNow.AddHours(-2))
                    .SetProperty(token => token.ExpiresAt, DateTime.UtcNow.AddHours(-1)));
            await Call("RECOVERY-EXPIRED", "/api/auth/reset-password", new { token = expiredToken, newPassword = "z", confirmPassword = "z" }, 400);
            await Call("RECOVERY-RATE-LIMIT", "/api/auth/forgot-password", request, 429);
            await recovery.RequestAsync(new("verify_customer", "wrong@example.test"), default);
            Check(capture.Count == 1, "Incorrect registered email produces no recovery message.");
            await recovery.RequestAsync(new("verify_customer", "Customer@example.test"), default);
            var replacedToken = capture.Token;
            await recovery.RequestAsync(new("verify_customer", "Customer@example.test"), default);
            await Reject("New request invalidates the previous link.", () => recovery.ResetAsync(new(replacedToken, "x", "x"), default));
            await Reject("Password confirmation must match.", () => recovery.ResetAsync(new(capture.Token, "x", "y"), default));
            var admin = await db.Users.AsNoTracking().SingleAsync(user => user.Role == "Admin");
            await new UserManagementService(db, hasher).ResetPasswordAsync(admin.Id, target.Id, new ResetUserPasswordRequest { NewPassword = "k" }, default);
            await Reject("An administrator password reset invalidates older recovery links.", () => recovery.ResetAsync(new(capture.Token, "x", "x"), default));
            var complete = new Dictionary<string, string>
            {
                ["username"] = "registration_missing", ["email"] = "required@example.test",
                ["fullName"] = "Required information", ["password"] = "x", ["confirmPassword"] = "x"
            };
            foreach (var field in complete.Keys)
            {
                var incomplete = new Dictionary<string, string>(complete); incomplete.Remove(field);
                await Call("REGISTER-MISSING-" + field, "/api/auth/register", incomplete, 400);
            }
            var escalation = new Dictionary<string, string>(complete) { ["role"] = "Admin" };
            await Call("REGISTER-ROLE-INJECTION", "/api/auth/register", escalation, 400);
            Check(!await db.Users.AnyAsync(user => user.Username == "registration_missing"), "Incomplete registration and role injection create no account.");
            passed = true;
        }
        finally
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new { observedAt = DateTimeOffset.UtcNow, passed, source = "REAL_API_POSTGRESQL_LOCAL_SMTP", checks }, new JsonSerializerOptions { WriteIndented = true }));
        }

        async Task<JsonNode?> Call(string name, string path, object body, int expected)
        {
            using var response = await http.PostAsJsonAsync(path, body);
            Check((int)response.StatusCode == expected, $"{name}: HTTP {(int)response.StatusCode}, expected {expected}.");
            var content = await response.Content.ReadAsStringAsync();
            return string.IsNullOrWhiteSpace(content) ? null : JsonNode.Parse(content);
        }
        void Check(bool condition, string name)
        {
            checks.Add(new { name, passed = condition });
            if (!condition) throw new InvalidOperationException(name);
            Console.WriteLine("PASS " + name);
        }
        async Task Reject(string name, Func<Task> action)
        {
            try { await action(); } catch (AppValidationException) { Check(true, name); return; }
            throw new InvalidOperationException(name);
        }
    }

    private static string ExtractToken(string mime)
    {
        var normalized = mime.Replace("\r\n", "\n");
        var body = normalized[(normalized.IndexOf("\n\n", StringComparison.Ordinal) + 2)..];
        if (Regex.IsMatch(normalized, @"Content-Transfer-Encoding:\s*base64", RegexOptions.IgnoreCase)) body = Encoding.UTF8.GetString(Convert.FromBase64String(body));
        var match = Regex.Match(body, @"reset-password#token=([0-9A-F]{64})");
        if (!match.Success) throw new InvalidOperationException("Reset credential missing from test SMTP message.");
        return match.Groups[1].Value;
    }

    private sealed class CapturingSender : IPasswordRecoveryEmailSender
    {
        public bool IsConfigured => true;
        public string Token { get; private set; } = "";
        public int Count { get; private set; }
        public Uri CreateResetLink(string token) => new("http://localhost/reset-password#token=" + token);
        public Task SendAsync(string email, string fullName, Uri resetLink, CancellationToken cancellationToken)
        {
            Token = resetLink.Fragment[7..]; Count++; return Task.CompletedTask;
        }
    }
}
