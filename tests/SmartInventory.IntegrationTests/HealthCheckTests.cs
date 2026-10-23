using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace SmartInventory.IntegrationTests;

public sealed class HealthCheckTests(InventoryApiFactory app) : IClassFixture<InventoryApiFactory>
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoints_are_anonymous_and_do_not_disclose_configuration(string path)
    {
        using var anonymous = app.Anonymous();
        using var response = await anonymous.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal(path == "/health/ready" ? "Ready" : "Healthy", body!["status"]!.GetValue<string>());
        Assert.DoesNotContain("connection", body.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", body.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", body.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", body.ToJsonString(), StringComparison.OrdinalIgnoreCase);
    }
}
