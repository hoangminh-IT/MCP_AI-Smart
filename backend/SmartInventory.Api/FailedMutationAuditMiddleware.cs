using System.Globalization;
using System.Security.Claims;
using SmartInventory.Application.Entities;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Api;

/// <summary>Records only authenticated failed business mutation metadata after the response is finalized.</summary>
public sealed class FailedMutationAuditMiddleware(RequestDelegate next, IServiceScopeFactory scopes,
    ILogger<FailedMutationAuditMiddleware> logger)
{
    private static readonly IReadOnlyDictionary<string, string> Entities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["warehouses"] = "Warehouse", ["users"] = "User", ["products"] = "Product", ["categories"] = "Category", ["suppliers"] = "Supplier",
        ["customers"] = "Customer", ["purchase-orders"] = "PurchaseOrder", ["sales-orders"] = "SalesOrder",
        ["inventory"] = "Inventory", ["stock-movements"] = "StockMovement", ["settings"] = "ApplicationSettings",
        ["preferences"] = "UserPreferences", ["auth"] = "User"
    };

    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);
        if (context.Response.StatusCode < 400 || context.User.Identity?.IsAuthenticated != true ||
            context.Request.Method is not ("POST" or "PUT" or "PATCH" or "DELETE") ||
            !int.TryParse(context.User.FindFirstValue("sub"), NumberStyles.None, CultureInfo.InvariantCulture, out var actorId)) return;
        var segments = context.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments is not { Length: >= 2 } || !string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase) ||
            !Entities.TryGetValue(segments[1], out var entity)) return;
        // Never persist arbitrary route text, body, query parameters, exception messages or submitted values.
        string? target = segments.Length > 2 && int.TryParse(segments[2], NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
            ? id.ToString(CultureInfo.InvariantCulture) : null;
        if (segments[1] is "auth" or "preferences") target = actorId.ToString(CultureInfo.InvariantCulture);
        if (segments[1] == "settings") target = "1";
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.AuditLogs.Add(new AuditLog { ActorId = actorId, EntityName = entity, TargetId = target,
                Action = context.Request.Method == "DELETE" ? "Deleted" : context.Request.Method is "PUT" or "PATCH" ? "Updated" : "Created",
                Status = "Failed", CreatedAt = DateTime.UtcNow });
            // The request may have been aborted after the mutation was rejected; keep the audit attempt bounded.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await database.SaveChangesAsync(timeout.Token);
        }
        catch (Exception exception)
        {
            logger.LogWarning("Failed mutation audit could not be stored ({ExceptionType}); trace {TraceId}.", exception.GetType().Name, context.TraceIdentifier);
        }
    }
}
