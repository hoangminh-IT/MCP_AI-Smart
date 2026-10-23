using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Mcp;
using SmartInventory.Infrastructure.Authentication;
using SmartInventory.Infrastructure.Catalog;
using SmartInventory.Infrastructure.Data;
using SmartInventory.Infrastructure.Mcp;
using SmartInventory.McpServer;
using SmartInventory.McpServer.Tools;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 64 * 1024);
builder.Services.AddProblemDetails();
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection through user secrets or environment variables.");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddInventoryAuthentication(builder.Configuration);
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<IProductService>(services => services.GetRequiredService<CatalogService>());
builder.Services.AddScoped<ICategoryService>(services => services.GetRequiredService<CatalogService>());
builder.Services.AddScoped<IInventoryReadService, InventoryReadService>();
builder.Services.AddScoped<IMcpExecutionLogService, McpExecutionLogService>();

var allowedOrigins = builder.Configuration.GetSection("Mcp:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:6274", "http://127.0.0.1:6274"];
foreach (var origin in allowedOrigins)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
        !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath != "/" ||
        !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || origin.EndsWith('/'))
        throw new InvalidOperationException("Mcp:AllowedOrigins must contain HTTP(S) origins without paths, credentials, queries or fragments.");
}
builder.Services.AddSingleton(new McpOriginPolicy(new HashSet<string>(allowedOrigins, StringComparer.Ordinal)));
builder.Services.AddCors(options => options.AddPolicy("McpInspector", policy => policy
    .WithOrigins(allowedOrigins)
    .WithMethods("GET", "POST", "DELETE", "OPTIONS")
    .WithHeaders("Authorization", "Content-Type", "Accept", "MCP-Protocol-Version", "MCP-Session-Id", "Last-Event-ID")
    .WithExposedHeaders("MCP-Session-Id", "WWW-Authenticate")));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
        await Results.Problem(statusCode: StatusCodes.Status429TooManyRequests,
            title: "Too many requests", detail: "Wait before issuing another MCP request.")
            .ExecuteAsync(context.HttpContext);
    options.AddPolicy("mcp", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirstValue("sub") ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
        }));
});

builder.Services.AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation { Name = "SmartInventory.McpServer", Version = "1.0.0" };
        options.ServerInstructions = "Read-only inventory tools use current PostgreSQL data. Tool errors are failures; never invent replacement inventory results.";
        options.ScopeRequests = true;
    })
    .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
    .WithRequestFilters(filters => filters.AddCallToolFilter(McpExecutionFilter.Wrap))
    .AddAuthorizationFilters()
    .WithTools<InventoryTools>();

var app = builder.Build();
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    SuppressDiagnosticsCallback = _ => true,
    ExceptionHandler = async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        app.Logger.LogError("MCP HTTP request failed with {ExceptionType}; trace {TraceId}.",
            exception?.GetType().Name ?? "Unknown", context.TraceIdentifier);
        await Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
            title: "Server error", detail: "The MCP request could not be completed. Try again later.")
            .ExecuteAsync(context);
    }
});
app.UseMiddleware<ServerSecurityMiddleware>();
app.UseRouting();
app.UseCors("McpInspector");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy", service = "SmartInventory.McpServer", transport = "Streamable HTTP", endpoint = "/mcp"
})).AllowAnonymous();
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();
app.MapMcp("/mcp")
    .RequireAuthorization(new AuthorizeAttribute { Roles = UserRoles.Admin + "," + UserRoles.Staff })
    .RequireRateLimiting("mcp");

app.Run();

public partial class Program { }
