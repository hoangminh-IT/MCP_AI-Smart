using System.Net.Http.Headers;
using System.Text;
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
    private const string RateLimitError = "The LLM provider returned HTTP 429 because the request rate limit was reached. Wait before trying again.";

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
        IReadOnlyList<McpToolDescriptor> tools, CancellationToken cancellationToken,
        Func<AiProviderWait, CancellationToken, Task>? onWait = null)
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
            ["stream"] = false,
            ["parallel_tool_calls"] = false
        };
        if (!string.IsNullOrWhiteSpace(options.ReasoningEffort))
            payload["reasoning_effort"] = options.ReasoningEffort.Trim();
        var requestPayload = payload.ToJsonString();
        if (requestPayload.Length > Math.Clamp(options.MaxContextCharacters, 8000, 128000))
            throw new AiProviderException("The conversation exceeds the configured context budget. Start a new conversation or narrow the tool query.");
        using var response = await SendCompletionAsync(requestPayload, cancellationToken, onWait);
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

    private async Task<HttpResponseMessage> SendCompletionAsync(string payload, CancellationToken cancellationToken,
        Func<AiProviderWait, CancellationToken, Task>? onWait)
    {
        var endpoint = new Uri(options.BaseUrl.TrimEnd('/') + "/chat/completions");
        var authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        var maximumRetries = Math.Clamp(options.MaxRateLimitRetries, 0, 1);
        for (var attempt = 0; ; attempt++)
        {
            TimeSpan retryDelay;
            using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            })
            {
                request.Headers.Authorization = authorization;
                var response = await SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode) return response;
                try
                {
                    var error = await ProviderErrorAsync(response, cancellationToken);
                    if (attempt >= maximumRetries || error != RateLimitError ||
                        !TryGetRetryDelay(response, out retryDelay))
                        throw new AiProviderException(error);
                }
                finally
                {
                    response.Dispose();
                }
            }
            // Retry only this rejected completion, never conversation orchestration or an MCP operation.
            // The caller supplies the original conversation deadline, including this wait.
            if (onWait is not null)
                await onWait(new(true, (int)Math.Ceiling(retryDelay.TotalSeconds)), cancellationToken);
            await Task.Delay(retryDelay, cancellationToken);
            if (onWait is not null) await onWait(new(false, 0), cancellationToken);
        }
    }

    private static bool TryGetRetryDelay(HttpResponseMessage response, out TimeSpan delay)
    {
        delay = default;
        if (!response.Headers.TryGetValues("Retry-After", out var values)) return false;
        var entries = values.Take(2).ToArray();
        if (entries.Length != 1 || !RetryConditionHeaderValue.TryParse(entries[0], out var retryAfter)) return false;
        if (retryAfter.Delta is { } delta) delay = delta;
        else if (retryAfter.Date is { } date) delay = date - DateTimeOffset.UtcNow;
        else return false;
        if (delay < TimeSpan.Zero || delay > TimeSpan.FromSeconds(60)) return false;
        // Timer delays use millisecond resolution; round up so an HTTP date is never retried early.
        delay = TimeSpan.FromMilliseconds(Math.Ceiling(delay.TotalMilliseconds));
        return true;
    }

    private static async Task<string> ProviderErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var fallback = $"The LLM provider returned HTTP {(int)response.StatusCode}. Check provider credentials, model access and service status.";
        if (response.StatusCode != System.Net.HttpStatusCode.TooManyRequests) return fallback;

        // Inspect only a bounded error code. Upstream messages, bodies and headers may contain secrets.
        const int maximumErrorBytes = 64 * 1024;
        if (response.Content.Headers.ContentLength > maximumErrorBytes) return fallback;
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > maximumErrorBytes) return fallback;
                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            }
            using var json = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 8 });
            if (json.RootElement.ValueKind != JsonValueKind.Object ||
                !json.RootElement.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
                return fallback;

            string? code = null;
            if (error.TryGetProperty("code", out var codeValue) && codeValue.ValueKind != JsonValueKind.Null)
            {
                if (codeValue.ValueKind == JsonValueKind.String) code = codeValue.GetString();
            }
            else if (error.TryGetProperty("type", out var typeValue) && typeValue.ValueKind == JsonValueKind.String)
            {
                // Older compatible endpoints sometimes omit code and provide only these known types.
                code = typeValue.GetString() switch
                {
                    "insufficient_quota" => "insufficient_quota",
                    "rate_limit_exceeded" or "rate_limit_error" => "rate_limit_exceeded",
                    _ => null
                };
            }
            return code switch
            {
                "credit_balance_exhausted" => "The LLM provider returned HTTP 429 because the API credit balance is exhausted. Add API credits before trying again.",
                "organization_spend_limit_exceeded" => "The LLM provider returned HTTP 429 because the organization spend limit was reached. Review the organization API spend limit before trying again.",
                "project_spend_limit_exceeded" => "The LLM provider returned HTTP 429 because the project spend limit was reached. Review the project API spend limit before trying again.",
                "organization_usage_limit_exceeded" => "The LLM provider returned HTTP 429 because the organization usage limit was reached. Request a higher API usage limit before trying again.",
                "insufficient_quota" => "The LLM provider returned HTTP 429 because API quota is unavailable. Check API billing and usage limits before trying again.",
                "rate_limit_exceeded" or "slow_down" => RateLimitError,
                _ => fallback
            };
        }
        catch (Exception exception) when (exception is JsonException or IOException or HttpRequestException)
        {
            return fallback;
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try { return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken); }
        catch (HttpRequestException) { throw new AiProviderException("The LLM provider could not be reached. Check its configured endpoint and network availability."); }
    }
}
