using Microsoft.AspNetCore.Mvc;

namespace SmartInventory.McpServer;

/// <summary>Restricts this local MCP resource server's Host and browser Origin headers.</summary>
public sealed class ServerSecurityMiddleware(RequestDelegate next, McpOriginPolicy policy)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var host = context.Request.Host.Host;
        if (!host.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
            host is not ("127.0.0.1" or "[::1]" or "::1"))
        {
            await RejectAsync(context, "This MCP server accepts requests to loopback hosts only.");
            return;
        }

        if (context.Request.Path.StartsWithSegments("/mcp") &&
            context.Request.Headers.TryGetValue("Origin", out var origins) &&
            (origins.Count != 1 || !policy.AllowedOrigins.Contains(origins[0] ?? string.Empty)))
        {
            await RejectAsync(context, "The request Origin is not permitted.");
            return;
        }

        await next(context);
    }

    private static async Task RejectAsync(HttpContext context, string detail)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Detail = detail
        }, options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}

public sealed record McpOriginPolicy(IReadOnlySet<string> AllowedOrigins);
