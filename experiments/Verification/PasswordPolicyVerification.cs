using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Entities;
using SmartInventory.Infrastructure.Data;

internal static class PasswordPolicyVerification
{
    public static async Task RunAsync(Uri address, string connection, string adminToken, string outputPath)
    {
        using var client = new HttpClient { BaseAddress = address };
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options;
        var hasher = new PasswordHasher<User>();
        var checks = new List<object>();
        var passed = false;
        try
        {
            const string shortPassword = "x";
            const string changedPassword = "  é🙂  ";
            var longPassword = new string('L', 256) + "🙂";
            var registered = await Request("REGISTER-ONE-CHAR", "/api/auth/register", new
            {
                username = "password_short", email = "short@example.test", fullName = "Password verification",
                password = shortPassword, confirmPassword = shortPassword
            }, 201);
            Assert(registered!["role"]!.GetValue<string>() == "Customer", "Registration still assigns Customer.");
            foreach (var blank in new[] { "", "   " })
                await Request("REGISTER-BLANK-REJECTED", "/api/auth/register", new
                {
                    username = "password_blank", email = "blank@example.test", fullName = "Invalid password",
                    password = blank, confirmPassword = blank
                }, 400);

            var shortLogin = await Request("LOGIN-ONE-CHAR", "/api/auth/login", new { username = "password_short", password = shortPassword }, 200);
            var oldToken = shortLogin!["accessToken"]!.GetValue<string>();
            await Request("CHANGE-SHORT-UNICODE", "/api/auth/change-password", new { currentPassword = shortPassword, newPassword = changedPassword }, 204, oldToken);
            await Request("CHANGE-REVOKES-OLD-TOKEN", "/api/auth/me", null, 401, oldToken);
            await Request("LOGIN-UNICODE-WHITESPACE", "/api/auth/login", new { username = "password_short", password = changedPassword }, 200);

            var saltUser = await Request("CREATE-ONE-CHAR", "/api/users", new
            {
                username = "password_salt", email = "salt@example.test", fullName = "Salt verification",
                role = "Staff", isActive = true, password = shortPassword
            }, 201, adminToken);
            var longUser = await Request("CREATE-LONG-PASSWORD", "/api/users", new
            {
                username = "password_long", email = "long@example.test", fullName = "Long password verification",
                role = "Staff", isActive = true, password = longPassword
            }, 201, adminToken);
            var longLogin = await Request("LOGIN-OVER-128-CHARS", "/api/auth/login", new { username = "password_long", password = longPassword }, 200);
            var longId = longUser!["id"]!.GetValue<int>();

            await using var database = new AppDbContext(options);
            var registeredUser = await database.Users.AsNoTracking().SingleAsync(user => user.Username == "password_short");
            var createdUser = await database.Users.AsNoTracking().SingleAsync(user => user.Id == saltUser!["id"]!.GetValue<int>());
            var beforeReset = await database.Users.AsNoTracking().SingleAsync(user => user.Id == longId);
            VerifyHash(registeredUser, changedPassword);
            Assert(hasher.VerifyHashedPassword(registeredUser, registeredUser.PasswordHash, changedPassword.Trim()) == PasswordVerificationResult.Failed,
                "Whitespace is preserved, not trimmed before hashing.");
            VerifyHash(createdUser, shortPassword);
            VerifyHash(beforeReset, longPassword);

            // Register a second account with the same password through the admin API;
            // its salt must differ even though both plaintext inputs are identical.
            var secondSalt = await Request("CREATE-SAME-PASSWORD", "/api/users", new
            {
                username = "password_salt_second", email = "salt2@example.test", fullName = "Second salt verification",
                role = "Staff", isActive = true, password = shortPassword
            }, 201, adminToken);
            var secondId = secondSalt!["id"]!.GetValue<int>();
            var secondUser = await database.Users.AsNoTracking().SingleAsync(user => user.Id == secondId);
            VerifyHash(secondUser, shortPassword);
            Assert(secondUser.PasswordHash != createdUser.PasswordHash, "Same password produces different salted hashes.");

            await Request("RESET-ONE-CHAR", $"/api/users/{longId}/reset-password", new { newPassword = "7" }, 204, adminToken);
            await Request("RESET-REVOKES-OLD-TOKEN", "/api/auth/me", null, 401, longLogin!["accessToken"]!.GetValue<string>());
            foreach (var blank in new[] { "", "   " })
                await Request("RESET-BLANK-REJECTED", $"/api/users/{longId}/reset-password", new { newPassword = blank }, 400, adminToken);
            var resetUser = await database.Users.AsNoTracking().SingleAsync(user => user.Id == longId);
            VerifyHash(resetUser, "7");
            Assert(resetUser.TokenVersion == beforeReset.TokenVersion + 1, "Password reset increments token version.");
            Assert(!await database.RefreshSessions.AnyAsync(session => session.UserId == longId && session.RevokedAt == null),
                "Password reset revokes refresh sessions.");
            Assert(hasher.VerifyHashedPassword(resetUser, resetUser.PasswordHash, longPassword) == PasswordVerificationResult.Failed,
                "Old password no longer verifies after reset.");
            Assert(!await database.Users.AnyAsync(user => user.Username == "password_blank"), "Invalid registration creates no account.");
            passed = true;
        }
        finally
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new
            {
                observedAt = DateTimeOffset.UtcNow, passed,
                source = "REAL_CSHARP_API_POSTGRESQL", checks
            }, new JsonSerializerOptions { WriteIndented = true }));
        }

        async Task<JsonNode?> Request(string name, string path, object? body, int expectedStatus, string? token = null)
        {
            using var request = new HttpRequestMessage(body is null ? HttpMethod.Get : HttpMethod.Post, path);
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await client.SendAsync(request);
            // Record outcomes only. Passwords, hashes, bearer tokens and response bodies stay in memory.
            checks.Add(new { name, expectedStatus, actualStatus = (int)response.StatusCode });
            if ((int)response.StatusCode != expectedStatus)
                throw new InvalidOperationException($"{name}: expected HTTP {expectedStatus}, received {(int)response.StatusCode}.");
            Console.WriteLine("PASS " + name);
            var text = await response.Content.ReadAsStringAsync();
            return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
        }

        void Assert(bool condition, string name)
        {
            checks.Add(new { name, passed = condition });
            if (!condition) throw new InvalidOperationException(name);
            Console.WriteLine("PASS " + name);
        }

        void VerifyHash(User user, string plaintext)
        {
            Assert(user.PasswordHash != plaintext && hasher.VerifyHashedPassword(user, user.PasswordHash, plaintext) != PasswordVerificationResult.Failed,
                "Stored password is a verifiable hash for " + user.Username);
        }
    }
}
