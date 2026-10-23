namespace SmartInventory.Api.Mcp;

public sealed class McpConnectionOptions
{
    public const string SectionName = "Mcp";
    public string ServerUrl { get; set; } = "http://localhost:5206/mcp";
    public int TimeoutSeconds { get; set; } = 30;
    public bool UseSharedFirstPartyAudience { get; set; }

    public bool HasTrustedEndpoint() => Uri.TryCreate(ServerUrl, UriKind.Absolute, out var endpoint) &&
        (endpoint.Scheme == "https" || endpoint.Scheme == "http" && endpoint.IsLoopback) &&
        string.IsNullOrEmpty(endpoint.UserInfo) && string.IsNullOrEmpty(endpoint.Query) && string.IsNullOrEmpty(endpoint.Fragment);
}
