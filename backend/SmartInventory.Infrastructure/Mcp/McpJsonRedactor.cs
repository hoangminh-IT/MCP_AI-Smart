using System.Text.Json;
using System.Text.Json.Nodes;

namespace SmartInventory.Infrastructure.Mcp;

public static class McpJsonRedactor
{
    private static readonly string[] SensitiveNames =
        ["password", "token", "secret", "authorization", "connectionstring", "apikey", "api_key", "cookie"];

    public static JsonElement Sanitize(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Undefined) return JsonSerializer.SerializeToElement(new { });
        var node = Visit(value, 0);
        return JsonSerializer.SerializeToElement(node);
    }

    private static JsonNode? Visit(JsonElement value, int depth)
    {
        if (depth > 24) return JsonValue.Create("[depth limit]");
        if (value.ValueKind == JsonValueKind.Object)
        {
            var result = new JsonObject();
            foreach (var property in value.EnumerateObject())
                result[property.Name] = SensitiveNames.Any(name => property.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                    ? JsonValue.Create("[redacted]") : Visit(property.Value, depth + 1);
            return result;
        }
        if (value.ValueKind == JsonValueKind.Array)
        {
            var result = new JsonArray();
            foreach (var item in value.EnumerateArray()) result.Add(Visit(item, depth + 1));
            return result;
        }
        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString() ?? string.Empty;
            if (text.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return JsonValue.Create("[redacted]");
            return JsonValue.Create(text.Length > 16000 ? text[..16000] + "[truncated]" : text);
        }
        return JsonNode.Parse(value.GetRawText());
    }
}
