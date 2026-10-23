using Microsoft.Extensions.Options;
using SmartInventory.Application.Mcp;
using SmartInventory.Infrastructure.Mcp;

namespace SmartInventory.Api.Mcp;

public static class DependencyInjectionMcpExtensions
{
    public static IServiceCollection AddInventoryMcpClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddOptions<McpConnectionOptions>().Bind(configuration.GetSection(McpConnectionOptions.SectionName))
            .Validate(options => options.HasTrustedEndpoint(), "Mcp:ServerUrl must be HTTPS or a loopback HTTP URL without credentials, query or fragment.")
            .Validate(options => options.TimeoutSeconds is >= 5 and <= 60, "Mcp:TimeoutSeconds must be between 5 and 60.")
            .Validate(options => options.UseSharedFirstPartyAudience,
                "Explicitly enable Mcp:UseSharedFirstPartyAudience only when API and MCP represent the same first-party JWT resource.")
            .ValidateOnStart();
        services.AddHttpClient("FirstPartyMcp", client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddScoped<IMcpClientService, McpClientService>();
        services.AddScoped<IMcpExecutionLogService, McpExecutionLogService>();
        return services;
    }
}
