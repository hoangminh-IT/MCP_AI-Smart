using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Ai;
using SmartInventory.Application.Mcp;
using SmartInventory.Infrastructure.Ai;
using Microsoft.Extensions.Options;
using Xunit;

namespace SmartInventory.IntegrationTests;

public sealed class AiOrchestrationTests
{
    // This explicitly scripted provider verifies orchestration. MCP transport and PostgreSQL remain real.
    private sealed class ScriptedProvider : ILlmProvider
    {
        public string Sku { get; set; } = "";
        public bool SawRealToolData { get; private set; }
        public int Calls { get; private set; }
        public AiProviderStatus GetStatus() => new(true, "Scripted test provider", "contract-only", null);
        public Task<AiCompletion> CompleteAsync(IReadOnlyList<AiProviderMessage> messages, IReadOnlyList<McpToolDescriptor> tools, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Calls++;
            Assert.Contains(tools, item => item.Name == "GetProductBySku");
            if (messages.Last().Role != "tool") return Task.FromResult(new AiCompletion(null,
                [new("contract-call", "GetProductBySku", JsonSerializer.Serialize(new { sku = Sku }))]));
            Assert.Contains(Sku, messages.Last().Content);
            Assert.Contains("Actual PostgreSQL Product", messages.Last().Content);
            SawRealToolData = true;
            return Task.FromResult(new AiCompletion("Contract workflow completed.", []));
        }
    }

    private sealed class IntentContractProvider(IReadOnlyDictionary<string, AiFunctionCall> scriptedCalls) : ILlmProvider
    {
        public int Calls { get; private set; }
        public bool SawRealMcpOutput { get; private set; }
        public AiProviderStatus GetStatus() => new(true, "Scripted intent contract", "contract-only", null);

        public Task<AiCompletion> CompleteAsync(IReadOnlyList<AiProviderMessage> messages,
            IReadOnlyList<McpToolDescriptor> tools, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            Assert.Equal("system", messages[0].Role);
            Assert.Contains("GetLowStockProducts with threshold N", messages[0].Content, StringComparison.Ordinal);
            Assert.Contains("GetStockMovementHistory", messages[0].Content, StringComparison.Ordinal);
            Assert.Contains("not sales revenue", messages[0].Content, StringComparison.Ordinal);
            Assert.Contains(tools, item => item.Name == "GetInventorySummary");
            Assert.Contains(tools, item => item.Name == "GetLowStockProducts");
            Assert.Contains(tools, item => item.Name == "SearchProducts");
            Assert.Contains(tools, item => item.Name == "GetStockMovementHistory");
            var latest = messages[^1];
            if (latest.Role == "tool")
            {
                using var output = JsonDocument.Parse(latest.Content!);
                Assert.True(output.RootElement.TryGetProperty("structuredContent", out _));
                SawRealMcpOutput = true;
                return Task.FromResult(new AiCompletion("Đã truy vấn dữ liệu kho qua MCP.", []));
            }

            Assert.Equal("user", latest.Role);
            Assert.True(scriptedCalls.TryGetValue(latest.Content!, out var call), "The test question must have an explicit scripted intent mapping.");
            return Task.FromResult(new AiCompletion(null, [call!]));
        }
    }

    private sealed class RepeatingToolProvider : ILlmProvider
    {
        public int Calls { get; private set; }
        public AiProviderStatus GetStatus() => new(true, "Scripted round-limit provider", "contract-only", null);
        public Task<AiCompletion> CompleteAsync(IReadOnlyList<AiProviderMessage> messages,
            IReadOnlyList<McpToolDescriptor> tools, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(new AiCompletion(null, [new("round-" + Calls, "GetInventorySummary", "{}") ]));
        }
    }

    private sealed class TimeoutProvider : ILlmProvider
    {
        public int Calls { get; private set; }
        public AiProviderStatus GetStatus() => new(true, "Scripted timeout provider", "contract-only", null);
        public async Task<AiCompletion> CompleteAsync(IReadOnlyList<AiProviderMessage> messages,
            IReadOnlyList<McpToolDescriptor> tools, CancellationToken cancellationToken)
        {
            Calls++;
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable scripted provider return.");
        }
    }

    [Fact]
    public async Task Scripted_llm_contract_calls_real_csharp_mcp_and_persists_observed_trace()
    {
        var provider = new ScriptedProvider();
        var app = new InventoryApiFactory { ProviderOverride = provider };
        try
        {
            await app.InitializeAsync();
            var categoryResponse = await app.Admin.PostAsJsonAsync("/api/categories", new { name = "AI contract category" });
            categoryResponse.EnsureSuccessStatusCode();
            var categoryId = (await categoryResponse.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>();
            provider.Sku = "AI-CONTRACT-" + Guid.NewGuid().ToString("N").ToUpperInvariant();
            (await app.Admin.PostAsJsonAsync("/api/products", new { categoryId, sku = provider.Sku, name = "Actual PostgreSQL Product", price = 125, costPrice = 50, minStock = 5 })).EnsureSuccessStatusCode();
            var create = await app.Admin.PostAsJsonAsync("/api/ai/sessions", new { title = "MCP orchestration contract" });
            create.EnsureSuccessStatusCode();
            var sessionId = (await create.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<string>();
            var send = await app.Admin.PostAsJsonAsync($"/api/ai/sessions/{sessionId}/messages", new { message = "Read product " + provider.Sku });
            send.EnsureSuccessStatusCode();
            var reply = (await send.Content.ReadFromJsonAsync<JsonObject>())!;
            Assert.True(provider.SawRealToolData);
            Assert.Equal(2, provider.Calls);
            var execution = Assert.Single(reply["toolExecutions"]!.AsArray())!;
            Assert.False(execution["isError"]!.GetValue<bool>());
            Assert.Equal("GetProductBySku", execution["toolName"]!.GetValue<string>());
            Assert.Contains(provider.Sku, execution["output"]!.ToJsonString());
            Assert.Contains(reply["trace"]!.AsArray(), step => step!["name"]!.GetValue<string>() == "MCP GetProductBySku");
            await using var database = app.NewContext();
            var session = await database.AiChatSessions.SingleAsync(item => item.Id == Guid.Parse(sessionId));
            Assert.Null(session.ProcessingToken);
            Assert.Null(session.ProcessingUntil);
            var messages = await database.AiChatMessages.Where(item => item.SessionId == session.Id).ToListAsync();
            Assert.Equal(3, messages.Count);
            Assert.Contains(messages, message => message.Role == "tool" && message.Content.Contains(provider.Sku));
            Assert.Contains(messages, message => message.Role == "assistant" && message.ToolMetadata!.Contains("executionIds"));

            // A lease held by another request must block a second paid provider call.
            await database.AiChatSessions.Where(item => item.Id == session.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ProcessingToken, Guid.NewGuid())
                    .SetProperty(item => item.ProcessingUntil, DateTime.UtcNow.AddMinutes(1)));
            var blocked = await app.Admin.PostAsJsonAsync($"/api/ai/sessions/{sessionId}/messages", new { message = "Another request" });
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
            Assert.Equal(2, provider.Calls);
        }
        finally { await ((IAsyncLifetime)app).DisposeAsync(); }
    }

    [Fact]
    public async Task Five_Vietnamese_inventory_questions_use_scripted_intent_and_real_MCP_PostgreSQL_data()
    {
        const string countQuestion = "Kho có bao nhiêu sản phẩm?";
        const string lowQuestion = "Sản phẩm nào còn dưới 10 chiếc?";
        const string searchQuestion = "Tìm sản phẩm Logitech.";
        const string valueQuestion = "Tổng giá trị tồn kho là bao nhiêu?";
        const string movementQuestion = "Cho tôi xem lịch sử nhập xuất gần nhất.";
        var calls = new Dictionary<string, AiFunctionCall>(StringComparer.Ordinal)
        {
            [countQuestion] = new("intent-count", "GetInventorySummary", "{}"),
            [lowQuestion] = new("intent-low", "GetLowStockProducts", "{\"threshold\":10,\"page\":1,\"pageSize\":10}"),
            [searchQuestion] = new("intent-search", "SearchProducts", "{\"search\":\"Logitech\",\"page\":1,\"pageSize\":10}"),
            [valueQuestion] = new("intent-value", "GetInventorySummary", "{}"),
            [movementQuestion] = new("intent-history", "GetStockMovementHistory", "{\"page\":1,\"pageSize\":5}")
        };
        var provider = new IntentContractProvider(calls);
        var app = new InventoryApiFactory { ProviderOverride = provider };
        try
        {
            await app.InitializeAsync();
            var categoryResponse = await app.Admin.PostAsJsonAsync("/api/categories", new { name = "AI M6 " + Guid.NewGuid().ToString("N") });
            categoryResponse.EnsureSuccessStatusCode();
            var categoryId = (await categoryResponse.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>();
            const string sku = "AI-M6-LOGITECH-001";
            var productResponse = await app.Admin.PostAsJsonAsync("/api/products", new
            {
                categoryId, sku, name = "Logitech MX AI M6 Test", price = 120m, costPrice = 70m, minStock = 10
            });
            productResponse.EnsureSuccessStatusCode();
            var product = (await productResponse.Content.ReadFromJsonAsync<JsonObject>())!;
            var receipt = await app.Admin.PostAsJsonAsync("/api/inventory/receipts", new
            {
                productId = product["id"]!.GetValue<int>(), quantity = 3, reason = "AI M6 real test receipt", requestId = Guid.NewGuid()
            });
            receipt.EnsureSuccessStatusCode();

            var expectedTools = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [countQuestion] = "GetInventorySummary",
                [lowQuestion] = "GetLowStockProducts",
                [searchQuestion] = "SearchProducts",
                [valueQuestion] = "GetInventorySummary",
                [movementQuestion] = "GetStockMovementHistory"
            };
            foreach (var question in expectedTools)
            {
                var create = await app.Admin.PostAsJsonAsync("/api/ai/sessions", new { title = "AI M6 scripted intent" });
                create.EnsureSuccessStatusCode();
                var sessionId = (await create.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<string>();
                var response = await app.Admin.PostAsJsonAsync($"/api/ai/sessions/{sessionId}/messages", new { message = question.Key });
                response.EnsureSuccessStatusCode();
                var reply = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
                var execution = Assert.Single(reply["toolExecutions"]!.AsArray())!;
                Assert.Equal(question.Value, execution["toolName"]!.GetValue<string>());
                Assert.False(execution["isError"]!.GetValue<bool>());
                Assert.Equal("Succeeded", execution["status"]!.GetValue<string>());
                Assert.True(execution["durationMs"]!.GetValue<long>() >= 0);

                var step = Assert.Single(reply["trace"]!.AsArray(), item => item!["name"]!.GetValue<string>() == "MCP " + question.Value);
                Assert.Equal(question.Value, step!["toolName"]!.GetValue<string>());
                Assert.Equal("Succeeded", step["status"]!.GetValue<string>());
                Assert.True(step["durationMs"]!.GetValue<long>() >= 0);
                Assert.NotNull(step["input"]);
                Assert.NotNull(step["output"]!["structuredContent"]);
                if (question.Key == lowQuestion) Assert.Equal(10, step["input"]!["threshold"]!.GetValue<int>());
                if (question.Key == searchQuestion)
                {
                    Assert.Equal("Logitech", step["input"]!["search"]!.GetValue<string>());
                    Assert.Contains(sku, step["output"]!.ToJsonString());
                }
                if (question.Key == movementQuestion) Assert.Contains("AI M6 real test receipt", step["output"]!.ToJsonString());
                if (question.Key == valueQuestion)
                {
                    var summary = step["output"]!["structuredContent"]!;
                    Assert.True(summary["inventoryRetailValue"]!.GetValue<decimal>() >= 360m);
                    Assert.True(summary["inventoryCostValue"]!.GetValue<decimal>() >= 210m);
                }
            }
            Assert.True(provider.SawRealMcpOutput);
            Assert.Equal(expectedTools.Count * 2, provider.Calls);
        }
        finally { await ((IAsyncLifetime)app).DisposeAsync(); }
    }

    [Fact]
    public async Task Failed_MCP_tool_is_not_followed_by_a_successful_AI_answer()
    {
        const string question = "Test MCP failure does not get fabricated";
        var provider = new IntentContractProvider(new Dictionary<string, AiFunctionCall>
        {
            [question] = new("intent-invalid", "GetLowStockProducts", "{\"threshold\":-1}")
        });
        var app = new InventoryApiFactory { ProviderOverride = provider };
        try
        {
            await app.InitializeAsync();
            var create = await app.Admin.PostAsJsonAsync("/api/ai/sessions", new { title = "MCP failure guard" });
            create.EnsureSuccessStatusCode();
            var sessionId = (await create.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<string>();
            var response = await app.Admin.PostAsJsonAsync($"/api/ai/sessions/{sessionId}/messages", new { message = question });
            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
            var conversation = (await app.Admin.GetFromJsonAsync<JsonObject>($"/api/ai/sessions/{sessionId}"))!;
            var failedAssistant = conversation["messages"]!.AsArray().Single(item => item!["role"]!.GetValue<string>() == "assistant");
            Assert.Equal("Failed", failedAssistant!["status"]!.GetValue<string>());
            var trace = failedAssistant["toolMetadata"]!["trace"]!.AsArray();
            var toolStep = Assert.Single(trace, item => item!["name"]!.GetValue<string>() == "MCP GetLowStockProducts");
            Assert.Equal("ToolError", toolStep!["status"]!.GetValue<string>());
            Assert.Equal(-1, toolStep["input"]!["threshold"]!.GetValue<int>());
            Assert.True(toolStep["output"]!["isError"]!.GetValue<bool>());
            Assert.True(provider.Calls == 1, "The provider must not be called again to invent an answer after a failed MCP call.");
        }
        finally { await ((IAsyncLifetime)app).DisposeAsync(); }
    }

    [Fact]
    public async Task Tool_round_limit_stops_MCP_and_provider_calls_at_the_configured_budget()
    {
        var provider = new RepeatingToolProvider();
        var app = new InventoryApiFactory { ProviderOverride = provider };
        LlmOptions? settings = null;
        var previousRoundLimit = 4;
        try
        {
            await app.InitializeAsync();
            settings = app.Services.GetRequiredService<IOptions<LlmOptions>>().Value;
            previousRoundLimit = settings.MaxToolRounds;
            settings.MaxToolRounds = 1;
            var create = await app.Admin.PostAsJsonAsync("/api/ai/sessions", new { title = "AI tool round budget" });
            create.EnsureSuccessStatusCode();
            var sessionId = (await create.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<string>();
            var response = await app.Admin.PostAsJsonAsync($"/api/ai/sessions/{sessionId}/messages", new { message = "Kho có bao nhiêu sản phẩm?" });
            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
            Assert.Equal(1, provider.Calls);
            await using var database = app.NewContext();
            Assert.Equal(1, await database.McpToolExecutionLogs.CountAsync(item => item.ToolName == "GetInventorySummary"));
        }
        finally
        {
            if (settings is not null) settings.MaxToolRounds = previousRoundLimit;
            await ((IAsyncLifetime)app).DisposeAsync();
        }
    }

    [Fact]
    public async Task AI_provider_timeout_is_persisted_in_trace_and_releases_conversation_lease()
    {
        var provider = new TimeoutProvider();
        var app = new InventoryApiFactory { ProviderOverride = provider };
        LlmOptions? settings = null;
        var previousTimeout = 60;
        try
        {
            await app.InitializeAsync();
            settings = app.Services.GetRequiredService<IOptions<LlmOptions>>().Value;
            previousTimeout = settings.RequestTimeoutSeconds;
            settings.RequestTimeoutSeconds = 10;

            var create = await app.Admin.PostAsJsonAsync("/api/ai/sessions", new { title = "AI provider timeout" });
            create.EnsureSuccessStatusCode();
            var sessionId = (await create.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<string>();
            var response = await app.Admin.PostAsJsonAsync($"/api/ai/sessions/{sessionId}/messages", new { message = "Kho có bao nhiêu sản phẩm?" });
            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
            Assert.Equal(1, provider.Calls);

            var conversation = (await app.Admin.GetFromJsonAsync<JsonObject>($"/api/ai/sessions/{sessionId}"))!;
            var failedAssistant = conversation["messages"]!.AsArray().Single(item => item!["role"]!.GetValue<string>() == "assistant");
            Assert.Equal("Timeout", failedAssistant!["status"]!.GetValue<string>());
            var timeoutStep = Assert.Single(failedAssistant["toolMetadata"]!["trace"]!.AsArray(), item => item!["name"]!.GetValue<string>() == "LLM response");
            Assert.Equal("Timeout", timeoutStep!["status"]!.GetValue<string>());
            Assert.True(timeoutStep["durationMs"]!.GetValue<long>() >= 9000);

            await using var database = app.NewContext();
            var session = await database.AiChatSessions.SingleAsync(item => item.Id == Guid.Parse(sessionId));
            Assert.Null(session.ProcessingToken);
            Assert.Null(session.ProcessingUntil);
        }
        finally
        {
            if (settings is not null) settings.RequestTimeoutSeconds = previousTimeout;
            await ((IAsyncLifetime)app).DisposeAsync();
        }
    }
}
