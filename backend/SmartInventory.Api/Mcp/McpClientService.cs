using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using SmartInventory.Application.Common;
using SmartInventory.Application.Mcp;

namespace SmartInventory.Api.Mcp;

/// <summary>A fresh official SDK transport/client per operation isolates caller credentials and bounds its lifetime.</summary>
public sealed class McpClientService(IHttpContextAccessor httpContextAccessor, IHttpClientFactory httpClients,
    IOptions<McpConnectionOptions> options, IMcpExecutionLogService logs, ILoggerFactory loggerFactory) : IMcpClientService
{
    public async Task<McpConnectionStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await WithClientAsync(async (client, token) =>
            {
                var tools = await client.ListToolsAsync(cancellationToken: token);
                var hasPermittedTools = tools.Any(tool => McpToolPolicy.IsAllowed(tool.Name));
                return new McpConnectionStatus(
                    !client.Completion.IsCompleted && hasPermittedTools, "Streamable HTTP", options.Value.ServerUrl,
                    client.ServerInfo?.Name, client.ServerInfo?.Version,
                    client.Completion.IsCompleted ? "The MCP client session closed after tool discovery."
                        : hasPermittedTools ? null : "The MCP server is reachable but advertises no permitted inventory tools.",
                    DateTime.UtcNow);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Disconnected("The MCP connection probe timed out.");
        }
        catch (Exception exception) when (exception is HttpRequestException or McpException or IOException)
        {
            return Disconnected("The MCP server is unavailable or rejected the connection.");
        }
    }

    public async Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await WithClientAsync(async (client, token) =>
            {
                var tools = await client.ListToolsAsync(cancellationToken: token);
                return (IReadOnlyList<McpToolDescriptor>)tools.Where(tool => McpToolPolicy.IsAllowed(tool.Name))
                    .Select(tool => new McpToolDescriptor(tool.Name, tool.Description, tool.JsonSchema.Clone(),
                        tool.ProtocolTool.Annotations is null ? null : JsonSerializer.SerializeToElement(tool.ProtocolTool.Annotations, McpJsonUtilities.DefaultOptions)))
                    .ToList();
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AppServiceUnavailableException("Listing MCP tools timed out.");
        }
        catch (Exception exception) when (exception is HttpRequestException or McpException or IOException)
        {
            throw new AppServiceUnavailableException("The MCP server is unavailable or rejected the connection.");
        }
    }

    public async Task<McpExecutionDto> ExecuteToolAsync(string toolName, JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId();
        var watch = Stopwatch.StartNew();
        string status;
        string? error;
        JsonElement? output;
        try
        {
            McpToolPolicy.ValidateArguments(toolName, arguments);
            var result = await WithClientAsync(async (client, token) =>
            {
                var available = await client.ListToolsAsync(cancellationToken: token);
                if (!available.Any(tool => tool.Name == toolName && McpToolPolicy.IsAllowed(tool.Name)))
                    throw new AppValidationException("The requested read tool is not available to this user.");
                var dictionary = arguments.EnumerateObject().ToDictionary(property => property.Name,
                    property => (object?)property.Value.Clone(), StringComparer.Ordinal);
                return await client.CallToolAsync(toolName, dictionary, cancellationToken: token);
            }, cancellationToken);
            watch.Stop();
            output = JsonSerializer.SerializeToElement(result, McpJsonUtilities.DefaultOptions);
            status = result.IsError is true ? McpExecutionStatuses.ToolError : McpExecutionStatuses.Succeeded;
            error = result.IsError is true ? SafeToolError(result) : null;
            if (result.Meta?["smartInventory/executionId"] is JsonValue identifier && identifier.TryGetValue<int>(out var executionId))
            {
                try { return await logs.GetAsync(userId, executionId, cancellationToken); }
                catch (AppNotFoundException) { /* Never expose another caller's audit record. */ }
            }
        }
        catch (AppValidationException exception)
        {
            status = McpExecutionStatuses.ValidationError;
            error = exception.Message;
            output = ErrorOutput(error);
        }
        catch (McpProtocolException exception)
        {
            status = McpExecutionStatuses.ProtocolError;
            error = $"The MCP protocol request failed ({exception.ErrorCode}).";
            output = ErrorOutput(error);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await SaveFallbackAsync(userId, toolName, arguments, ErrorOutput("The caller cancelled this tool request."),
                McpExecutionStatuses.Cancelled, watch.ElapsedMilliseconds, "The caller cancelled this tool request.");
            throw;
        }
        catch (OperationCanceledException)
        {
            status = McpExecutionStatuses.Timeout;
            error = "The MCP operation timed out.";
            output = ErrorOutput(error);
        }
        catch (Exception exception) when (exception is HttpRequestException or McpException or IOException)
        {
            status = McpExecutionStatuses.TransportError;
            error = "The MCP server is unavailable or rejected the connection.";
            output = ErrorOutput(error);
        }
        return await SaveFallbackAsync(userId, toolName, arguments, output, status, watch.ElapsedMilliseconds, error);
    }

    private async Task<T> WithClientAsync<T>(Func<ModelContextProtocol.Client.McpClient, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        var bearer = ReadCallerToken();
        var httpClient = httpClients.CreateClient("FirstPartyMcp");
        await using var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(settings.ServerUrl), TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = "Bearer " + bearer },
            EnableStandaloneGetStream = false
        }, httpClient, loggerFactory, ownsHttpClient: true);
        await using var client = await ModelContextProtocol.Client.McpClient.CreateAsync(transport,
            new McpClientOptions { InitializationTimeout = TimeSpan.FromSeconds(settings.TimeoutSeconds) },
            loggerFactory, deadline.Token);
        return await operation(client, deadline.Token);
    }

    private async Task<McpExecutionDto> SaveFallbackAsync(int userId, string toolName, JsonElement input,
        JsonElement? output, string status, long duration, string? error)
    {
        // Persist cancellation/timeout outcomes with a separate bounded token after the request token expires.
        using var auditDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var id = await logs.AppendAsync(userId, toolName, input, output, status, duration, error, auditDeadline.Token);
        return await logs.GetAsync(userId, id, auditDeadline.Token);
    }

    private int CurrentUserId() => int.TryParse(httpContextAccessor.HttpContext?.User.FindFirstValue("sub"),
        NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
        ? id : throw new AppUnauthorizedException("A valid user identity is required.");

    private string ReadCallerToken()
    {
        var header = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (header is null || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) || header.Length <= 7)
            throw new AppUnauthorizedException("A valid sign-in token is required for MCP calls.");
        return header[7..];
    }

    private McpConnectionStatus Disconnected(string error) => new(false, "Streamable HTTP", options.Value.ServerUrl,
        null, null, error, DateTime.UtcNow);

    private static string SafeToolError(CallToolResult result)
    {
        var error = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "The MCP tool reported an error.";
        return error.Length > 1000 ? error[..1000] : error;
    }

    private static JsonElement ErrorOutput(string error) => JsonSerializer.SerializeToElement(new
    {
        content = new[] { new { type = "text", text = error } }, structuredContent = (object?)null, isError = true
    });
}
