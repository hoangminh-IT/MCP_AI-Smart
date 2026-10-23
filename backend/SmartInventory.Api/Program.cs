using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using SmartInventory.Api;
using SmartInventory.Api.Mcp;
using SmartInventory.Api.Ai;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Mcp;
using SmartInventory.Application.Stock;
using SmartInventory.Infrastructure.Catalog;
using SmartInventory.Infrastructure.Authentication;
using SmartInventory.Infrastructure.Data;
using SmartInventory.Infrastructure.Mcp;
using SmartInventory.Infrastructure.Stock;

var seed = args.Contains("--seed", StringComparer.Ordinal);
var bootstrapAdmin = args.Contains("--bootstrap-admin", StringComparer.Ordinal);
var hostArgs = args.Where(argument => argument is not "--seed" and not "--bootstrap-admin").ToArray();
var builder = WebApplication.CreateBuilder(hostArgs);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection using user secrets or environment variables.")));
builder.Services.AddInventoryAuthentication(builder.Configuration);
builder.Services.AddInventoryMcpClient(builder.Configuration);
builder.Services.AddInventoryAi(builder.Configuration);
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<IProductService>(services => services.GetRequiredService<CatalogService>());
builder.Services.AddScoped<ICategoryService>(services => services.GetRequiredService<CatalogService>());
builder.Services.AddScoped<IInventoryReadService, InventoryReadService>();
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<IProductCsvService, ProductCsvService>();
builder.Services.AddScoped<IProductImageService, ProductImageService>();
builder.Services.AddScoped<DevelopmentCatalogSeeder>();
var imageRoot = Path.GetFullPath(builder.Configuration["ProductImages:RootPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "uploads", "products"));
builder.Services.AddSingleton(new ProductImageStorageOptions(imageRoot));

builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState.Where(entry => entry.Value?.Errors.Count > 0).ToDictionary(
            entry => JsonNamingPolicy.CamelCase.ConvertName(entry.Key),
            entry => entry.Value!.Errors.Select(error => string.IsNullOrEmpty(error.ErrorMessage)
                ? "The submitted value is invalid." : error.ErrorMessage).ToArray());
        var problem = new ValidationProblemDetails(errors)
        {
            Status = 400, Title = "Validation failed", Detail = "One or more submitted values are invalid."
        };
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    };
});
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
    .AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirstValue("sub") ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
            }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = 429, Title = "Too many requests", Detail = "The request limit was reached. Try again later."
        }, options: null, contentType: "application/problem+json", cancellationToken);
    };
});

var app = builder.Build();

if (seed || bootstrapAdmin)
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException("Catalog seed and administrator bootstrap commands require the Development environment.");
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if ((await database.Database.GetPendingMigrationsAsync()).Any())
        throw new InvalidOperationException("Apply pending database migrations before running development commands.");
    if (bootstrapAdmin)
    {
        var result = await scope.ServiceProvider.GetRequiredService<IAdminBootstrapService>().BootstrapAsync();
        app.Logger.LogInformation("Administrator bootstrap completed; created {Created}", result.Created);
    }
    if (seed)
    {
        var count = await scope.ServiceProvider.GetRequiredService<DevelopmentCatalogSeeder>().SeedAsync(CancellationToken.None);
        app.Logger.LogInformation("Development catalog seed completed; created {ProductCount} products with zero stock", count);
    }
    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    await response.WriteAsJsonAsync(new ProblemDetails
    {
        Status = response.StatusCode, Title = "Request failed",
        Detail = "The requested resource is unavailable or the request is not allowed."
    }, options: null, contentType: "application/problem+json", cancellationToken: context.HttpContext.RequestAborted);
});
if (app.Environment.IsDevelopment()) app.MapOpenApi();
else app.UseHttpsRedirection();
app.UseCors();
Directory.CreateDirectory(imageRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imageRoot), RequestPath = "/uploads/products",
    OnPrepareResponse = context => context.Context.Response.Headers.XContentTypeOptions = "nosniff"
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" })).DisableRateLimiting();
app.MapGet("/health/ready", async (AppDbContext database, CancellationToken cancellationToken) =>
{
    try
    {
        var connected = await database.Database.CanConnectAsync(cancellationToken);
        if (!connected || (await database.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
            return Results.Json(new { status = "Not ready" }, statusCode: 503);
        return Results.Ok(new { status = "Ready" });
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        app.Logger.LogWarning("Readiness probe failed with {ExceptionType}", exception.GetType().Name);
        return Results.Json(new { status = "Not ready" }, statusCode: 503);
    }
}).DisableRateLimiting();
app.Run();

public partial class Program;
