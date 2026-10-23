using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using SmartInventory.Application.Ai;
using SmartInventory.Application.Mcp;

namespace SmartInventory.Infrastructure.Ai;

/// <summary>Chat Completions adapter for providers exposing OpenAI-compatible function calling.</summary>
public sealed class OpenAiCompatibleProvider(HttpClient client, IOptions<LlmOptions> configuration) : ILlmProvider
{
    private readonly LlmOptions options = configuration.Value;

    public AiProviderStatus GetStatus()
    {
        string? reason = null;
        if (!string.Equals(options.Provider, "OpenAICompatible", StringComparison.OrdinalIgnoreCase))
            reason = "The configured provider has no registered adapter.";
        else if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) ||
                 !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
                 !(uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback))
            reason = "Configure Llm:BaseUrl as an HTTPS API base URL, or HTTP on loopback for local development.";
        else if (string.IsNullOrWhiteSpace(options.Model)) reason = "Configure Llm:Model with a model supporting Chat Completions tool calling.";
        else if (string.IsNullOrWhiteSpace(options.ApiKey)) reason = "Configure Llm:ApiKey through User Secrets or environment variables. MCP Direct Test Mode remains available.";
        return new(reason is null, options.Provider, options.Model, reason);
    }

    public async Task<AiCompletion> CompleteAsync(IReadOnlyList<AiProviderMessage> messages,
        IReadOnlyList<McpToolDescriptor> tools, CancellationToken cancellationToken)
    {
        var status = GetStatus();
        if (!status.Configured) throw new AiUnavailableException(status.Reason!);
        var wireMessages = new JsonArray();
        foreach (var message in messages)
        {
            var item = new JsonObject { ["role"] = message.Role, ["content"] = message.Content };
            if (message.ToolCallId is not null) item["tool_call_id"] = message.ToolCallId;
            if (message.ToolCalls is { Count: > 0 })
                item["tool_calls"] = new JsonArray(message.ToolCalls.Select(call => (JsonNode)new JsonObject
                {
                    ["id"] = call.Id, ["type"] = "function",
                    ["function"] = new JsonObject { ["name"] = call.Name, ["arguments"] = call.Arguments }
                }).ToArray());
            wireMessages.Add(item);
        }
        var wireTools = new JsonArray(tools.Select(tool => (JsonNode)new JsonObject
        {
            ["type"] = "function", ["function"] = new JsonObject
            {
                ["name"] = tool.Name, ["description"] = tool.Description,
                ["parameters"] = JsonNode.Parse(tool.InputSchema.GetRawText())
            }
        }).ToArray());
        var payload = new JsonObject
        {
            ["model"] = options.Model, ["messages"] = wireMessages, ["tools"] = wireTools,
            ["tool_choice"] = messages.Any(message => message.Role == "tool") ? "auto" : "required",
            ["max_completion_tokens"] = Math.Clamp(options.MaxOutputTokens, 128, 4096),
            ["stream"] = false
        };
        if (payload.ToJsonString().Length > Math.Clamp(options.MaxContextCharacters, 8000, 128000))
            throw new AiProviderException("The conversation exceeds the configured context budget. Start a new conversation or narrow the tool query.");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(options.BaseUrl.TrimEnd('/') + "/chat/completions"))
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        using var response = await SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            // Never log or expose upstream bodies: they may echo credentials or sensitive provider configuration.
            throw new AiProviderException($"The LLM provider returned HTTP {(int)response.StatusCode}. Check provider credentials, model access and service status.");
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > 1024 * 1024) throw new AiProviderException("The provider response exceeds the supported size.");
                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            }
            using var json = JsonDocument.Parse(buffer.ToArray());
            var choice = json.RootElement.GetProperty("choices")[0];
            if (choice.TryGetProperty("finish_reason", out var finish) && finish.GetString() is "length" or "content_filter")
                throw new AiProviderException("The provider stopped without a complete answer. Narrow the request or adjust the output budget.");
            var message = choice.GetProperty("message");
            var content = message.TryGetProperty("content", out var text) && text.ValueKind == JsonValueKind.String ? text.GetString() : null;
            var calls = new List<AiFunctionCall>();
            if (message.TryGetProperty("tool_calls", out var items) && items.ValueKind == JsonValueKind.Array)
                foreach (var item in items.EnumerateArray())
                {
                    if (item.GetProperty("type").GetString() != "function") throw new AiProviderException("The provider requested an unsupported tool type.");
                    var function = item.GetProperty("function");
                    var id = item.GetProperty("id").GetString();
                    var name = function.GetProperty("name").GetString();
                    var arguments = function.GetProperty("arguments").GetString();
                    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(arguments))
                        throw new AiProviderException("The provider returned an incomplete function call.");
                    calls.Add(new(id, name, arguments));
                }
            if (calls.Count == 0 && string.IsNullOrWhiteSpace(content)) throw new AiProviderException("The provider returned an empty answer.");
            return new(content, calls);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException or IOException or HttpRequestException)
        {
            throw new AiProviderException("The provider returned an invalid Chat Completions response.");
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try { return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken); }
        catch (HttpRequestException) { throw new AiProviderException("The LLM provider could not be reached. Check its configured endpoint and network availability."); }
    }
}
