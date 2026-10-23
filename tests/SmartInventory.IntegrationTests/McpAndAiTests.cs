using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SmartInventory.Api.Mcp;
using Xunit;

namespace SmartInventory.IntegrationTests;

public sealed class McpAndAiTests(InventoryApiFactory app) : IClassFixture<InventoryApiFactory>
{
    private async Task<JsonObject> ExecuteAsync(string name, object arguments, HttpClient? client = null)
    {
        var response = await (client ?? app.Admin).PostAsJsonAsync($"/api/mcp/tools/{name}/execute", new { arguments });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!;
    }

    [Fact]
    public async Task Csharp_sdk_connects_and_discovers_real_read_tools_without_llm_key()
    {
        var status = (await app.Admin.GetFromJsonAsync<JsonObject>("/api/mcp/status"))!;
        Assert.True(status["connected"]!.GetValue<bool>());
        Assert.Equal("Streamable HTTP", status["transport"]!.GetValue<string>());
        Assert.Null(status["errorMessage"]);
        var tools = (await app.Admin.GetFromJsonAsync<JsonArray>("/api/mcp/tools"))!;
        var names = tools.Select(item => item!["name"]!.GetValue<string>()).ToArray();
        Assert.Equal(10, names.Length);
        Assert.Contains("GetInventorySummary", names);
        Assert.Contains("GetLowStockProducts", names);
        Assert.Contains("GetAllProducts", names);
        Assert.Contains("SearchProducts", names);
        Assert.All(tools, item => Assert.Equal("object", item!["inputSchema"]!["type"]!.GetValue<string>()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(100)]
    public async Task Low_stock_thresholds_execute_over_real_http(int threshold)
    {
        var execution = await ExecuteAsync("GetLowStockProducts", new { threshold });
        Assert.False(execution["isError"]!.GetValue<bool>());
        Assert.Equal("Succeeded", execution["status"]!.GetValue<string>());
        Assert.NotNull(execution["output"]);
        Assert.True(execution["executionId"]!.GetValue<int>() > 0);
        if (threshold == 0)
            Assert.Empty(execution["output"]!["structuredContent"]!["items"]!.AsArray());
    }

    [Fact]
    public async Task Sku_tool_returns_product_persisted_through_rest_api()
    {
        var categoryResponse = await app.Admin.PostAsJsonAsync("/api/categories", new { name = "MCP " + Guid.NewGuid().ToString("N") });
        categoryResponse.EnsureSuccessStatusCode();
        var category = (await categoryResponse.Content.ReadFromJsonAsync<JsonObject>())!;
        var sku = "SDK-" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        (await app.Admin.PostAsJsonAsync("/api/products", new { categoryId = category["id"]!.GetValue<int>(), sku, name = "Real SDK result", price = 50, costPrice = 20, minStock = 5 })).EnsureSuccessStatusCode();
        var execution = await ExecuteAsync("GetProductBySku", new { sku });
        Assert.False(execution["isError"]!.GetValue<bool>());
        Assert.Contains(sku, execution["output"]!.ToJsonString());
        Assert.Contains("Real SDK result", execution["output"]!.ToJsonString());

        var allProducts = await ExecuteAsync("GetAllProducts", new { page = 1, pageSize = 100 });
        Assert.False(allProducts["isError"]!.GetValue<bool>());
        Assert.Contains(sku, allProducts["output"]!["structuredContent"]!["items"]!.ToJsonString());

        var search = await ExecuteAsync("SearchProducts", new { search = sku, page = 1, pageSize = 10 });
        Assert.False(search["isError"]!.GetValue<bool>());
        Assert.Contains(sku, search["output"]!["structuredContent"]!["items"]!.ToJsonString());

        var lowStock = await ExecuteAsync("GetLowStockProducts", new { threshold = 1, page = 1, pageSize = 10 });
        Assert.False(lowStock["isError"]!.GetValue<bool>());
        Assert.Equal(1, lowStock["input"]!["threshold"]!.GetValue<int>());
        Assert.Contains(sku, lowStock["output"]!["structuredContent"]!["items"]!.ToJsonString());

        var summary = await ExecuteAsync("GetInventorySummary", new { });
        Assert.False(summary["isError"]!.GetValue<bool>());
        Assert.True(summary["output"]!["structuredContent"]!["totalProducts"]!.GetValue<int>() > 0);
    }

    [Fact]
    public async Task Invalid_threshold_type_negative_unknown_and_write_tools_are_errors()
    {
        var negative = await ExecuteAsync("GetLowStockProducts", new { threshold = -1 });
        Assert.True(negative["isError"]!.GetValue<bool>());
        var type = await ExecuteAsync("GetLowStockProducts", new { threshold = "invalid" });
        Assert.True(type["isError"]!.GetValue<bool>());
        var unknown = await ExecuteAsync("ToolDoesNotExist", new { });
        Assert.True(unknown["isError"]!.GetValue<bool>());
        var write = await ExecuteAsync("CreateStockIssue", new { productId = 1, quantity = 1 });
        Assert.True(write["isError"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Mcp_results_and_execution_history_are_scoped_to_the_authenticated_user()
    {
        var staffExecution = await ExecuteAsync("GetCategorySummary", new { }, app.Staff);
        var id = staffExecution["executionId"]!.GetValue<int>();
        Assert.Equal(HttpStatusCode.NotFound, (await app.Admin.GetAsync($"/api/mcp/executions/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.Staff.GetAsync($"/api/mcp/executions/{id}")).StatusCode);
        var history = (await app.Staff.GetFromJsonAsync<JsonObject>("/api/mcp/executions?page=1&pageSize=20"))!;
        Assert.Contains(history["items"]!.AsArray(), item => item!["executionId"]!.GetValue<int>() == id);
        using var anonymous = app.Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/mcp/tools")).StatusCode);
    }

    [Fact]
    public async Task Unknown_sensitive_arguments_are_rejected_and_redacted_in_persisted_logs()
    {
        const string marker = "synthetic-sensitive-marker";
        var result = await ExecuteAsync("GetInventorySummary", new { apiKey = marker });
        Assert.True(result["isError"]!.GetValue<bool>());
        Assert.DoesNotContain(marker, result.ToJsonString());
        Assert.Contains("redacted", result["input"]!.ToJsonString());
    }

    [Fact]
    public async Task Unreachable_mcp_endpoint_reports_disconnected_while_rest_remains_available()
    {
        var settings = app.Services.GetRequiredService<IOptions<McpConnectionOptions>>().Value;
        var previous = settings.ServerUrl;
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        settings.ServerUrl = $"http://127.0.0.1:{port}/mcp";
        try
        {
            var status = (await app.Admin.GetFromJsonAsync<JsonObject>("/api/mcp/status"))!;
            Assert.False(status["connected"]!.GetValue<bool>());
            var execution = await ExecuteAsync("GetInventorySummary", new { });
            Assert.True(execution["isError"]!.GetValue<bool>());
            Assert.Equal("TransportError", execution["status"]!.GetValue<string>());
            Assert.Equal(HttpStatusCode.OK, (await app.Admin.GetAsync("/api/products")).StatusCode);
        }
        finally { settings.ServerUrl = previous; }
    }

    [Fact]
    public async Task Mcp_client_timeout_is_returned_and_audited_without_fabricating_output()
    {
        var settings = app.Services.GetRequiredService<IOptions<McpConnectionOptions>>().Value;
        var previousUrl = settings.ServerUrl;
        var previousTimeout = settings.TimeoutSeconds;
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        settings.ServerUrl = $"http://127.0.0.1:{port}/mcp";
        settings.TimeoutSeconds = 5;

        try
        {
            var pending = ExecuteAsync("GetInventorySummary", new { });
            using var accepted = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));
            var execution = await pending.WaitAsync(TimeSpan.FromSeconds(12));
            Assert.True(execution["isError"]!.GetValue<bool>());
            Assert.Equal("Timeout", execution["status"]!.GetValue<string>());
            Assert.True(execution["executionId"]!.GetValue<int>() > 0);
            Assert.Contains("timed out", execution["errorMessage"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
            Assert.Null(execution["output"]!["structuredContent"]);
        }
        finally
        {
            settings.ServerUrl = previousUrl;
            settings.TimeoutSeconds = previousTimeout;
            listener.Stop();
        }
    }

    [Fact]
    public async Task Direct_mcp_endpoint_requires_authentication_and_rejects_untrusted_origin()
    {
        using var client = new HttpClient();
        var response = await client.PostAsync(app.McpUrl, JsonContent.Create(new { jsonrpc = "2.0", id = 1, method = "server/discover", @params = new { } }));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Post, app.McpUrl) { Content = JsonContent.Create(new { }) };
        request.Headers.Add("Origin", "https://untrusted.example.test");
        var denied = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Missing_llm_key_is_explicit_and_does_not_break_catalog_or_direct_mcp()
    {
        var status = (await app.Admin.GetFromJsonAsync<JsonObject>("/api/ai/status"))!;
        Assert.False(status["configured"]!.GetValue<bool>());
        var created = await app.Admin.PostAsJsonAsync("/api/ai/sessions", new { title = "No provider configured" });
        created.EnsureSuccessStatusCode();
        var session = (await created.Content.ReadFromJsonAsync<JsonObject>())!;
        var id = session["id"]!.GetValue<string>();
        var send = await app.Admin.PostAsJsonAsync($"/api/ai/sessions/{id}/messages", new { message = "How many products?" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, send.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.Admin.GetAsync("/api/products")).StatusCode);
        Assert.False((await ExecuteAsync("GetInventorySummary", new { }))["isError"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Chat_sessions_enforce_ownership_rename_and_delete()
    {
        var created = await app.Staff.PostAsJsonAsync("/api/ai/sessions", new { title = "Staff conversation" });
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<string>();
        Assert.Equal(HttpStatusCode.NotFound, (await app.Admin.GetAsync($"/api/ai/sessions/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await app.Admin.DeleteAsync($"/api/ai/sessions/{id}")).StatusCode);
        (await app.Staff.PutAsJsonAsync($"/api/ai/sessions/{id}", new { title = "Renamed conversation" })).EnsureSuccessStatusCode();
        var renamed = (await app.Staff.GetFromJsonAsync<JsonObject>($"/api/ai/sessions/{id}"))!;
        Assert.Equal("Renamed conversation", renamed["session"]!["title"]!.GetValue<string>());
        Assert.Empty(renamed["messages"]!.AsArray());
        Assert.Equal(HttpStatusCode.NoContent, (await app.Staff.DeleteAsync($"/api/ai/sessions/{id}")).StatusCode);
    }
}
