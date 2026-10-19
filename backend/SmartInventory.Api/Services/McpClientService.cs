
using ModelContextProtocol.Client;

namespace SmartInventory.Api.Services;

public class McpClientService
{
    private readonly IConfiguration _configuration;

    public McpClientService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<List<string>> GetToolNamesAsync(
        CancellationToken cancellationToken = default)
    {
        // Lay dia chi MCP Server
        var endpoint = _configuration["Mcp:Endpoint"];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException(
                "Chua cau hinh Mcp:Endpoint");
        }

        // Tao ket noi MCP
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(endpoint),
                TransportMode = HttpTransportMode.StreamableHttp
            });

        await using var client =
            await McpClient.CreateAsync(
                transport,
                cancellationToken: cancellationToken);

        // Lay danh sach Tools
        var tools = await client.ListToolsAsync(
            cancellationToken: cancellationToken);

        return tools.Select(tool => tool.Name).ToList();
    }
}
