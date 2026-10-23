using System.Diagnostics;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SmartInventory.Application.Common;
using SmartInventory.Application.Mcp;

namespace SmartInventory.McpServer;

/// <summary>Audits actual SDK tool invocations, including requests issued by MCP Inspector.</summary>
public static class McpExecutionFilter
{
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Wrap(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next) => async (context, cancellationToken) =>
    {
        var started = Stopwatch.GetTimestamp();
        var toolName = context.Params?.Name ?? string.Empty;
        var input = context.Params?.Arguments is { } arguments
            ? JsonSerializer.SerializeToElement(arguments, McpJsonUtilities.DefaultOptions)
            : JsonSerializer.SerializeToElement(new Dictionary<string, object?>());
        var services = context.Services ?? throw new InvalidOperationException("MCP request services are unavailable.");
        var logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("SmartInventory.McpServer.ToolExecution");
        var userId = CurrentUserId(context.User);
        CallToolResult? result = null;
        var status = McpExecutionStatuses.Succeeded;
        string? errorMessage = null;

        try
        {
            if (userId is null)
                throw new AppUnauthorizedException("A valid signed-in user is required.");
            McpToolPolicy.ValidateArguments(toolName, input);
            result = await next(context, cancellationToken);
            if (result.IsError is true)
            {
                status = McpExecutionStatuses.ToolError;
                errorMessage = TrimError(result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text
                    ?? "The MCP tool reported an error.");
            }
            return result;
        }
        catch (AppValidationException exception)
        {
            status = McpExecutionStatuses.ValidationError;
            errorMessage = TrimError(exception.Message);
            return result = ErrorResult(errorMessage);
        }
        catch (AppNotFoundException exception)
        {
            status = McpExecutionStatuses.ToolError;
            errorMessage = TrimError(exception.Message);
            return result = ErrorResult(errorMessage);
        }
        catch (AppUnauthorizedException)
        {
            status = McpExecutionStatuses.ToolError;
            errorMessage = "A valid signed-in user is required.";
            return result = ErrorResult(errorMessage);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            status = McpExecutionStatuses.Cancelled;
            errorMessage = "The MCP tool request was cancelled.";
            throw;
        }
        catch (McpProtocolException)
        {
            status = McpExecutionStatuses.ProtocolError;
            errorMessage = "The MCP request could not be processed.";
            throw;
        }
        catch (Exception exception)
        {
            status = McpExecutionStatuses.ToolError;
            errorMessage = "The MCP tool could not be completed. Try again later.";
            logger.LogError("MCP tool {ToolName} failed with {ExceptionType}.", toolName, exception.GetType().Name);
            return result = ErrorResult(errorMessage);
        }
        finally
        {
            var durationMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (userId is not null)
            {
                try
                {
                    // An aborted caller must not prevent the server from recording an attempted execution.
                    using var auditDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    var output = result is null ? (JsonElement?)null
                        : JsonSerializer.SerializeToElement(result, McpJsonUtilities.DefaultOptions);
                    var audit = services.GetRequiredService<IMcpExecutionLogService>();
                    var executionId = await audit.AppendAsync(userId.Value, toolName, input, output,
                        status, durationMs, errorMessage, auditDeadline.Token);
                    if (result is not null)
                    {
                        result.Meta ??= new JsonObject();
                        result.Meta["smartInventory/executionId"] = executionId;
                    }
                }
                catch (Exception exception)
                {
                    // Database failures can make persistence unavailable. Keep credentials and SQL out of logs/results.
                    logger.LogWarning("MCP audit persistence failed for {ToolName} with {ExceptionType}; execution status {Status}.",
                        toolName, exception.GetType().Name, status);
                }
            }
        }
    };

    private static int? CurrentUserId(ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated is true &&
        int.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), NumberStyles.None,
            CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;

    private static string TrimError(string value) => value.Length <= 500 ? value : value[..500];

    private static CallToolResult ErrorResult(string detail) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = detail }],
        StructuredContent = JsonSerializer.SerializeToElement(new { error = detail })
    };
}
