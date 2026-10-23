using System.Text.Json;
using SmartInventory.Application.Common;

namespace SmartInventory.Application.Mcp;

/// <summary>Explicit read-only allowlist shared by first-party client and server.</summary>
public static class McpToolPolicy
{
    private static readonly IReadOnlyDictionary<string, string[]> Parameters = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["GetAllProducts"] = ["page", "pageSize"],
        ["GetProductById"] = ["id"],
        ["GetProductBySku"] = ["sku"],
        ["SearchProducts"] = ["search", "page", "pageSize"],
        ["GetProductsByCategory"] = ["categoryId", "page", "pageSize"],
        ["GetLowStockProducts"] = ["threshold", "page", "pageSize"],
        ["GetOutOfStockProducts"] = ["page", "pageSize"],
        ["GetInventorySummary"] = [],
        ["GetStockMovementHistory"] = ["productId", "page", "pageSize"],
        ["GetCategorySummary"] = []
    };

    public static bool IsAllowed(string toolName) => Parameters.ContainsKey(toolName);

    public static void ValidateArguments(string toolName, JsonElement arguments)
    {
        if (!Parameters.TryGetValue(toolName, out var allowed))
            throw new AppValidationException("This tool is not in the permitted read-only tool list.");
        if (arguments.ValueKind != JsonValueKind.Object)
            throw new AppValidationException("Tool arguments must be a JSON object.");
        if (System.Text.Encoding.UTF8.GetByteCount(arguments.GetRawText()) > 16 * 1024)
            throw new AppValidationException("Tool arguments must be at most 16 KiB.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in arguments.EnumerateObject())
        {
            if (!seen.Add(property.Name))
                throw new AppValidationException($"Duplicate tool parameter '{property.Name}'.");
            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
                throw new AppValidationException($"Unknown tool parameter '{property.Name}'.");
            if (property.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
                throw new AppValidationException($"Tool parameter '{property.Name}' must be a scalar value.");
        }
    }
}
