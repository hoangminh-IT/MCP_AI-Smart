using System.Text.Json;

namespace SmartInventory.Application.Mcp;

public sealed record McpResourceDescriptor(string Uri, string Name, string? Description, string? MimeType);
public sealed record McpPromptArgument(string Name, string? Description, bool Required);
public sealed record McpPromptDescriptor(string Name, string? Description, IReadOnlyList<McpPromptArgument> Arguments);

/// <summary>First-party documentation URIs and prompt names; never accepts arbitrary files or network URLs.</summary>
public static class McpKnowledgePolicy
{
    public const string StockPolicyUri = "inventory://docs/stock-policy";
    public const string ArchitectureUri = "inventory://docs/architecture";
    public const string AnalyzeLowStock = "AnalyzeLowStock";
    public const string InventoryReport = "InventoryReport";
    public static bool IsResourceAllowed(string uri) => uri is StockPolicyUri or ArchitectureUri;
    public static bool IsPromptAllowed(string name) => name is AnalyzeLowStock or InventoryReport;
}

public interface IMcpKnowledgeClient
{
    Task<IReadOnlyList<McpResourceDescriptor>> ListResourcesAsync(CancellationToken cancellationToken = default);
    Task<JsonElement> ReadResourceAsync(string uri, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<McpPromptDescriptor>> ListPromptsAsync(CancellationToken cancellationToken = default);
    Task<JsonElement> GetPromptAsync(string name, JsonElement arguments, CancellationToken cancellationToken = default);
}
