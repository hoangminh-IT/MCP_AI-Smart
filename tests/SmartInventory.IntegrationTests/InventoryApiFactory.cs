using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using SmartInventory.Application.Entities;
using SmartInventory.Application.Ai;
using SmartInventory.Infrastructure.Data;
using Xunit;

namespace SmartInventory.IntegrationTests;

// Each run uses its own schema in the configured PostgreSQL database. Public data is never changed.
public sealed class InventoryApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string schema = "inventory_test_" + Guid.NewGuid().ToString("N");
    private string connectionString = "";
    private bool schemaCreated;
    private readonly string imageRoot = Path.Combine(Path.GetTempPath(), "inventory-images-" + Guid.NewGuid().ToString("N"));
    private readonly string signingKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private readonly McpServerProcess mcpServer = new();
    public string McpUrl => mcpServer.Url;
    public ILlmProvider? ProviderOverride { get; init; }
    public const string Password = "Integration-Password-2026!";
    public HttpClient Admin { get; private set; } = null!;
    public HttpClient Staff { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString,
            ["Jwt:SigningKey"] = signingKey,
            ["Jwt:Issuer"] = "inventory-integration-tests",
            ["Jwt:Audience"] = "inventory-integration-tests",
            ["Jwt:AccessTokenMinutes"] = "30",
            ["ProductImages:RootPath"] = imageRoot,
            ["Mcp:ServerUrl"] = mcpServer.Url,
            ["Mcp:TimeoutSeconds"] = "5",
            ["Mcp:UseSharedFirstPartyAudience"] = "true",
            ["Llm:ApiKey"] = "",
            ["Logging:LogLevel:Default"] = "Warning",
            ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Warning"
        };
        builder.UseEnvironment("Testing");
        foreach (var (key, value) in settings) builder.UseSetting(key, value);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
        if (ProviderOverride is not null) builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ILlmProvider>();
            services.AddSingleton(ProviderOverride);
        });
    }

    public AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(connectionString).Options);

    public async Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables().Build();
        var configured = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException("Integration tests require ConnectionStrings__DefaultConnection or API development user secrets. No database simulation is used.");
        var source = new NpgsqlConnectionStringBuilder(configured);
        await using (var connection = new NpgsqlConnection(source.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", connection);
            await command.ExecuteNonQueryAsync();
            schemaCreated = true;
        }
        source.SearchPath = schema;
        source.IncludeErrorDetail = false;
        connectionString = source.ConnectionString;
        await using (var database = NewContext()) await database.Database.MigrateAsync();
        await AddUserAsync("integration_admin", "Admin");
        await AddUserAsync("integration_staff", "Staff");
        await mcpServer.StartAsync(connectionString, signingKey);
        Admin = await LoginAsync("integration_admin");
        Staff = await LoginAsync("integration_staff");
    }

    public async Task AddUserAsync(string username, string role = "Staff")
    {
        await using var database = NewContext();
        var user = new User
        {
            Username = username, Role = role, Email = username + "@example.test",
            FullName = "Integration User", IsActive = true
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, Password);
        database.Users.Add(user);
        await database.SaveChangesAsync();
    }

    public HttpClient Anonymous() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false
    });

    public async Task<HttpClient> LoginAsync(string username, string password = Password)
    {
        var client = Anonymous();
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json!["accessToken"]!.GetValue<string>());
        return client;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        Admin?.Dispose();
        Staff?.Dispose();
        await base.DisposeAsync();
        await mcpServer.DisposeAsync();
        if (!schemaCreated) return;
        // The name is generated above, never supplied by configuration or user input.
        if (!System.Text.RegularExpressions.Regex.IsMatch(schema, "^inventory_test_[0-9a-f]{32}$"))
            throw new InvalidOperationException("Unexpected integration schema name.");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", connection);
        await command.ExecuteNonQueryAsync();
        if (Directory.Exists(imageRoot)) Directory.Delete(imageRoot, recursive: true);
    }
}
