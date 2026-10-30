using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using SmartInventory.McpServer.Tools;

namespace SmartInventory.McpServer.Schema;

/// <summary>Preserves decimal validation bounds as JSON numbers in SDK-generated tool schemas.</summary>
internal static class DecimalRangeToolRegistration
{
    private static readonly AIJsonSchemaCreateOptions SchemaOptions = new()
    {
        TransformSchemaNode = static (context, node) =>
        {
            if (node is not JsonObject schema ||
                (Nullable.GetUnderlyingType(context.TypeInfo.Type) ?? context.TypeInfo.Type) != typeof(decimal))
                return node;

            // Range(typeof(decimal), string, string) retains exact decimal limits in validation.
            // The schema exporter also retains those strings; JSON Schema requires numeric bounds.
            foreach (var keyword in new[] { "minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum" })
            {
                if (schema[keyword] is not JsonValue value || !value.TryGetValue<string>(out var text))
                    continue;
                if (!decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowExponent,
                        CultureInfo.InvariantCulture, out var bound))
                    throw new InvalidOperationException("A decimal tool schema bound is not a valid decimal number.");
                schema[keyword] = JsonValue.Create(bound);
            }
            return node;
        }
    };

    /// <summary>Uses the SDK's normal tool factory with a decimal schema correction at creation time.</summary>
    public static IMcpServerBuilder WithInventoryWriteTools(this IMcpServerBuilder builder)
    {
        foreach (var method in typeof(WriteTools).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (method.GetCustomAttribute<McpServerToolAttribute>() is null) continue;
            builder.Services.AddSingleton<McpServerTool>(services => McpServerTool.Create(method,
                options: new McpServerToolCreateOptions
                {
                    Services = services,
                    SchemaCreateOptions = SchemaOptions
                }));
        }
        return builder;
    }
}
