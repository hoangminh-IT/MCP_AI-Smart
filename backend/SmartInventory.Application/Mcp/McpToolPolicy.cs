using System.Text.Json;
using SmartInventory.Application.Common;

namespace SmartInventory.Application.Mcp;

/// <summary>Explicit tool allowlist. Write tools require a separate server-consumed user confirmation.</summary>
public static class McpToolPolicy
{
    private static readonly IReadOnlyDictionary<string, string[]> Parameters = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["GetAllProducts"] = ["page", "pageSize", "warehouseId"],
        ["GetProductById"] = ["id", "warehouseId"],
        ["GetProductBySku"] = ["sku", "warehouseId"],
        ["SearchProducts"] = ["search", "page", "pageSize", "warehouseId"],
        ["GetProductsByCategory"] = ["categoryId", "page", "pageSize", "warehouseId"],
        ["GetLowStockProducts"] = ["threshold", "page", "pageSize", "warehouseId"],
        ["GetOutOfStockProducts"] = ["page", "pageSize", "warehouseId"],
        ["GetInventorySummary"] = ["warehouseId"],
        ["GetStockMovementHistory"] = ["productId", "page", "pageSize", "warehouseId"],
        ["GetCategorySummary"] = ["warehouseId"],
        ["GetSuppliers"] = ["isActive", "page", "pageSize"],
        ["SearchSuppliers"] = ["search", "isActive", "page", "pageSize"],
        ["GetCustomers"] = ["isActive", "page", "pageSize"],
        ["SearchCustomers"] = ["search", "isActive", "page", "pageSize"],
        ["GetPurchaseOrders"] = ["search", "supplierId", "status", "page", "pageSize", "warehouseId"],
        ["GetPurchaseOrderById"] = ["id"],
        ["GetPurchaseOrderStatus"] = ["id"],
        ["GetSalesOrders"] = ["search", "customerId", "status", "page", "pageSize", "warehouseId"],
        ["GetSalesOrderById"] = ["id"],
        ["GetSalesOrderStatus"] = ["id"],
        ["GetStockMovements"] = ["productId", "type", "from", "to", "page", "pageSize", "warehouseId"],
        ["GetPurchaseSummary"] = ["from", "to", "warehouseId"],
        ["GetSalesSummary"] = ["from", "to", "warehouseId"],
        ["GetWarehouses"] = ["search", "isActive", "page", "pageSize"],
        ["GetSupplierProducts"] = ["supplierId", "page", "pageSize", "warehouseId"]
    };

    public static bool IsAllowed(string toolName) => Parameters.ContainsKey(toolName) || IsWrite(toolName);
    public static bool IsWrite(string toolName) => McpWritePayload.IsWriteTool(toolName);
    public static bool IsReadOnly(string toolName) => Parameters.ContainsKey(toolName);

    public static void ValidateArguments(string toolName, JsonElement arguments)
    {
        if (IsWrite(toolName))
        {
            McpWritePayload.Normalize(toolName, arguments);
            return;
        }
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
