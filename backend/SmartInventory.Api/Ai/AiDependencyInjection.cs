using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using SmartInventory.Application.Ai;
using SmartInventory.Infrastructure.Ai;

namespace SmartInventory.Api.Ai;

public static class AiDependencyInjection
{
    public static IServiceCollection AddInventoryAi(this IServiceCollection services, IConfiguration configuration)
    {
        // Optional configuration: missing LLM credentials never prevents API or MCP startup.
        services.Configure<LlmOptions>(configuration.GetSection("Llm"));
        services.AddHttpClient<ILlmProvider, OpenAiCompatibleProvider>(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
            .RedactLoggedHeaders(["Authorization"]);
        services.AddScoped<IAiChatService, AiChatService>();
        services.AddRateLimiter(options => options.AddPolicy("ai", context => RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst("sub")?.Value ?? "anonymous", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
            })));
        return services;
    }
}
