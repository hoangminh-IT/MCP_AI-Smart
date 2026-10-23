using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SmartInventory.Application.Ai;
using SmartInventory.Application.Mcp;
using SmartInventory.Infrastructure.Ai;
using Xunit;

namespace SmartInventory.IntegrationTests;

// Scripted HTTP responses test the provider contract only. They are not live LLM validation or simulated MCP results.
public sealed class LlmProviderContractTests
{
    private static LlmOptions Settings() => new()
    {
        BaseUrl = "https://provider.example.test/v1", Model = "contract-test-model", ApiKey = "synthetic-contract-key"
    };
    private static readonly AiProviderMessage[] Messages = [new("user", "Inventory summary")];
    private static readonly McpToolDescriptor[] Tools = [new("GetInventorySummary", "Inventory summary", JsonSerializer.SerializeToElement(new { type = "object", properties = new { } }), null)];

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => callback(request);
    }
    private static HttpResponseMessage Response(string json, HttpStatusCode status = HttpStatusCode.OK) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    [Fact]
    public async Task Missing_key_never_sends_an_upstream_request()
    {
        var settings = Settings(); settings.ApiKey = "";
        var called = false;
        var client = new HttpClient(new Handler(_ => { called = true; return Task.FromResult(Response("{}")); }));
        var provider = new OpenAiCompatibleProvider(client, Options.Create(settings));
        Assert.False(provider.GetStatus().Configured);
        await Assert.ThrowsAsync<AiUnavailableException>(() => provider.CompleteAsync(Messages, Tools, CancellationToken.None));
        Assert.False(called);
    }

    [Fact]
    public async Task Adapter_sends_discovered_schema_and_parses_function_selection()
    {
        var client = new HttpClient(new Handler(async request =>
        {
            Assert.Equal("https://provider.example.test/v1/chat/completions", request.RequestUri!.ToString());
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal("required", body.RootElement.GetProperty("tool_choice").GetString());
            Assert.Equal("GetInventorySummary", body.RootElement.GetProperty("tools")[0].GetProperty("function").GetProperty("name").GetString());
            return Response("""{"choices":[{"finish_reason":"tool_calls","message":{"content":null,"tool_calls":[{"id":"contract-call","type":"function","function":{"name":"GetInventorySummary","arguments":"{}"}}]}}]}""");
        }));
        var provider = new OpenAiCompatibleProvider(client, Options.Create(Settings()));
        var completion = await provider.CompleteAsync(Messages, Tools, CancellationToken.None);
        Assert.Equal("GetInventorySummary", Assert.Single(completion.ToolCalls).Name);
    }

    [Fact]
    public async Task Adapter_preserves_Vietnamese_question_and_sends_discovered_tool_schema()
    {
        const string question = "Sản phẩm nào còn dưới 10 chiếc?";
        var messages = new AiProviderMessage[] { new("system", "Trả lời bằng tiếng Việt."), new("user", question) };
        var tools = new McpToolDescriptor[]
        {
            new("GetLowStockProducts", "Find products under stock threshold.",
                JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = new { threshold = new { type = "integer", minimum = 0 } },
                    required = new[] { "threshold" }
                }), null)
        };
        var client = new HttpClient(new Handler(async request =>
        {
            Assert.Equal("Bearer synthetic-contract-key", request.Headers.Authorization!.ToString());
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal(question, body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString());
            Assert.Equal("required", body.RootElement.GetProperty("tool_choice").GetString());
            var parameters = body.RootElement.GetProperty("tools")[0].GetProperty("function").GetProperty("parameters");
            Assert.Equal("GetLowStockProducts", body.RootElement.GetProperty("tools")[0].GetProperty("function").GetProperty("name").GetString());
            Assert.Equal("integer", parameters.GetProperty("properties").GetProperty("threshold").GetProperty("type").GetString());
            return Response("""{"choices":[{"finish_reason":"tool_calls","message":{"content":null,"tool_calls":[{"id":"low-stock-call","type":"function","function":{"name":"GetLowStockProducts","arguments":"{\"threshold\":10}"}}]}}]}""");
        }));
        var provider = new OpenAiCompatibleProvider(client, Options.Create(Settings()));

        var completion = await provider.CompleteAsync(messages, tools, CancellationToken.None);

        var selected = Assert.Single(completion.ToolCalls);
        Assert.Equal("GetLowStockProducts", selected.Name);
        using var arguments = JsonDocument.Parse(selected.Arguments);
        Assert.Equal(10, arguments.RootElement.GetProperty("threshold").GetInt32());
    }

    [Theory]
    [InlineData("http://provider.example.test/v1", "model", "key")]
    [InlineData("https://provider.example.test/v1?token=not-allowed", "model", "key")]
    [InlineData("https://provider.example.test/v1", "", "key")]
    [InlineData("https://provider.example.test/v1", "model", "")]
    public void Provider_status_rejects_incomplete_or_untrusted_configuration(string baseUrl, string model, string apiKey)
    {
        var settings = new LlmOptions { BaseUrl = baseUrl, Model = model, ApiKey = apiKey };
        var provider = new OpenAiCompatibleProvider(new HttpClient(), Options.Create(settings));

        Assert.False(provider.GetStatus().Configured);
        Assert.False(string.IsNullOrWhiteSpace(provider.GetStatus().Reason));
    }

    [Fact]
    public void Provider_status_allows_loopback_http_for_local_compatible_servers()
    {
        var settings = new LlmOptions { BaseUrl = "http://127.0.0.1:11434/v1", Model = "local-test", ApiKey = "local" };
        var provider = new OpenAiCompatibleProvider(new HttpClient(), Options.Create(settings));

        Assert.True(provider.GetStatus().Configured);
    }

    [Fact]
    public async Task Null_function_arguments_are_a_controlled_provider_error()
    {
        var client = new HttpClient(new Handler(_ => Task.FromResult(Response("""{"choices":[{"message":{"tool_calls":[{"id":"bad","type":"function","function":{"name":"GetInventorySummary","arguments":null}}]}}]}"""))));
        var provider = new OpenAiCompatibleProvider(client, Options.Create(Settings()));
        await Assert.ThrowsAsync<AiProviderException>(() => provider.CompleteAsync(Messages, Tools, CancellationToken.None));
    }

    [Fact]
    public async Task Network_failure_and_provider_body_do_not_expose_secrets()
    {
        var client = new HttpClient(new Handler(_ => throw new HttpRequestException("synthetic-private-network-detail")));
        var provider = new OpenAiCompatibleProvider(client, Options.Create(Settings()));
        var network = await Assert.ThrowsAsync<AiProviderException>(() => provider.CompleteAsync(Messages, Tools, CancellationToken.None));
        Assert.DoesNotContain("synthetic-private-network-detail", network.Message);
        client = new HttpClient(new Handler(_ => Task.FromResult(Response("synthetic-provider-secret", HttpStatusCode.Unauthorized))));
        provider = new(client, Options.Create(Settings()));
        var error = await Assert.ThrowsAsync<AiProviderException>(() => provider.CompleteAsync(Messages, Tools, CancellationToken.None));
        Assert.DoesNotContain("synthetic-provider-secret", error.Message);
        Assert.Contains("401", error.Message);
    }
}
