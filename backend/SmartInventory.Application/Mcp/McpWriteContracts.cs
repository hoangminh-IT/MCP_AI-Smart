using System.Globalization;
using System.Text;
using System.Text.Json;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Common;
using SmartInventory.Application.Purchasing;

namespace SmartInventory.Application.Mcp;

public sealed record McpWriteIntentDto(Guid Id, string ToolName, JsonElement Arguments, string Status,
    DateTime CreatedAt, DateTime ExpiresAt, Guid? SessionId, JsonElement? Result, string? ErrorMessage);

/// <summary>Private service result. The proof must never be returned to a browser, model or log.</summary>
public sealed record McpWriteApproval(McpWriteIntentDto Intent, string Proof);

public interface IMcpWriteIntentService
{
    Task<McpWriteIntentDto> CreateAsync(int userId, string toolName, JsonElement arguments, Guid? sessionId, CancellationToken cancellationToken);
    Task<McpWriteIntentDto> GetAsync(int userId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<McpWriteIntentDto>> ListAsync(int userId, Guid? sessionId, CancellationToken cancellationToken);
    Task<McpWriteApproval> ApproveAsync(int userId, Guid id, CancellationToken cancellationToken);
    Task<McpWriteIntentDto> CancelAsync(int userId, Guid id, CancellationToken cancellationToken);
}

public interface IMcpWriteGuard
{
    Task<JsonElement> ExecuteAsync(int userId, string toolName, JsonElement arguments, string confirmationToken,
        Func<CancellationToken, Task<JsonElement>> operation, CancellationToken cancellationToken);
}

public static class McpWriteStatuses
{
    public const string Pending = "Pending", Approved = "Approved", Succeeded = "Succeeded",
        Cancelled = "Cancelled", Expired = "Expired";
}

/// <summary>Strict, bounded business payloads shared by intent preparation and actual MCP execution.</summary>
public static class McpWritePayload
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] StockFields = ["productId", "quantity", "reason", "requestId", "warehouseId"];
    public const string ConfirmationHeader = "X-Inventory-Confirmation";

    public static bool IsWriteTool(string name) => name is "CreateStockReceipt" or "CreateStockIssue" or
        "AdjustInventory" or "CreatePurchaseOrder" or "UpdatePurchaseOrderStatus";

    public static JsonElement Normalize(string toolName, JsonElement arguments, Guid? generatedRequestId = null)
    {
        if (!IsWriteTool(toolName)) throw new AppValidationException("This is not a permitted write tool.");
        if (arguments.ValueKind != JsonValueKind.Object || Encoding.UTF8.GetByteCount(arguments.GetRawText()) > 16 * 1024)
            throw new AppValidationException("Write arguments must be an object of at most 16 KiB.");
        ValidateDuplicates(arguments, 0);
        object normalized;
        if (toolName is "CreateStockReceipt" or "CreateStockIssue" or "AdjustInventory")
        {
            Allowed(arguments, StockFields);
            var productId = Integer(arguments, "productId");
            var quantity = Integer(arguments, "quantity");
            var reason = Text(arguments, "reason")?.Trim() ?? string.Empty;
            var requestId = generatedRequestId ?? Guid.Empty;
            if (arguments.TryGetProperty("requestId", out var request) && request.ValueKind != JsonValueKind.Null)
            {
                if (request.ValueKind != JsonValueKind.String || !Guid.TryParse(request.GetString(), out requestId))
                    throw new AppValidationException("requestId must be a UUID.");
                if (requestId == Guid.Empty && generatedRequestId.HasValue) requestId = generatedRequestId.Value;
            }
            if (productId <= 0 || quantity < 0 || toolName != "AdjustInventory" && quantity == 0 ||
                reason.Length is < 1 or > 1000 || requestId == Guid.Empty)
                throw new AppValidationException("Use a positive productId, a nonempty requestId, a 1 to 1,000 character reason and a positive amount or nonnegative adjustment target.");
            normalized = new { productId, quantity, reason, requestId };
        }
        else if (toolName == "CreatePurchaseOrder")
        {
            Allowed(arguments, ["supplierId", "notes", "items", "warehouseId"]);
            if (!arguments.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array ||
                items.GetArrayLength() is < 1 or > 100)
                throw new AppValidationException("Purchase orders require 1 to 100 item objects.");
            var lines = new List<PurchaseItemRequest>();
            foreach (var line in items.EnumerateArray())
            {
                if (line.ValueKind != JsonValueKind.Object) throw new AppValidationException("Every purchase item must be an object.");
                Allowed(line, ["productId", "quantity", "unitCost"]);
                if (!line.TryGetProperty("unitCost", out var cost) ||
                    !decimal.TryParse(cost.ValueKind is JsonValueKind.String ? cost.GetString() : cost.GetRawText(),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out var unitCost))
                    throw new AppValidationException("unitCost must be a decimal amount with at most two fractional digits.");
                lines.Add(new PurchaseItemRequest { ProductId = Integer(line, "productId"), Quantity = Integer(line, "quantity"), UnitCost = unitCost });
            }
            var request = new PurchaseOrderRequest { SupplierId = Integer(arguments, "supplierId"), Notes = Text(arguments, "notes"), Items = lines };
            PurchaseRules.ValidateOrder(request);
            normalized = new { supplierId = request.SupplierId, notes = request.Notes,
                items = request.Items.OrderBy(line => line.ProductId).Select(line => new { productId = line.ProductId,
                    quantity = line.Quantity, unitCost = decimal.Parse(line.UnitCost.ToString("0.00", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) }).ToArray() };
        }
        else
        {
            Allowed(arguments, ["id", "status"]);
            var id = Integer(arguments, "id");
            var status = Text(arguments, "status")?.Trim();
            status = new[] { PurchaseStatuses.Submitted, PurchaseStatuses.Approved, PurchaseStatuses.Cancelled }
                .SingleOrDefault(value => value.Equals(status, StringComparison.OrdinalIgnoreCase));
            if (id <= 0 || status is null)
                throw new AppValidationException("Use a positive id and status Submitted, Approved or Cancelled. Receiving goods uses its separate workflow.");
            normalized = new { id, status };
        }
        var normalizedJson = JsonSerializer.SerializeToElement(normalized, JsonOptions);
        // Preserve historical omission canonical hashes: omitted/null warehouse means MAIN.
        // An explicitly selected warehouse is part of the proof-bound canonical payload.
        if (arguments.TryGetProperty("warehouseId", out var scope) && scope.ValueKind != JsonValueKind.Null)
        {
            var warehouseId = Integer(arguments, "warehouseId");
            if (warehouseId <= 0) throw new AppValidationException("warehouseId must be positive.");
            var fields = normalizedJson.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);
            fields["warehouseId"] = JsonSerializer.SerializeToElement(warehouseId);
            normalizedJson = JsonSerializer.SerializeToElement(fields, JsonOptions);
        }
        var result = Canonical(normalizedJson);
        if (Encoding.UTF8.GetByteCount(result.GetRawText()) > 16 * 1024)
            throw new AppValidationException("Normalized write arguments exceed 16 KiB.");
        return result;
    }

    public static void RequirePermission(string role, string toolName, JsonElement arguments)
    {
        if (!UserRoles.CanManageInventory(role)) throw new AppForbiddenException("An active inventory operator account is required.");
        if ((toolName == "AdjustInventory" || toolName == "UpdatePurchaseOrderStatus" &&
            arguments.GetProperty("status").GetString() == PurchaseStatuses.Approved) && role != UserRoles.Admin)
            throw new AppForbiddenException("Only administrators may perform this confirmed operation.");
    }

    private static JsonElement Canonical(JsonElement value)
    {
        using var memory = new MemoryStream();
        using (var writer = new Utf8JsonWriter(memory)) Write(value, writer);
        using var document = JsonDocument.Parse(memory.ToArray());
        return document.RootElement.Clone();
    }

    private static void Write(JsonElement value, Utf8JsonWriter writer)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
            { writer.WritePropertyName(property.Name); Write(property.Value, writer); }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in value.EnumerateArray()) Write(item, writer);
            writer.WriteEndArray();
        }
        else value.WriteTo(writer);
    }

    private static void Allowed(JsonElement value, IReadOnlyCollection<string> names)
    {
        if (value.EnumerateObject().Any(property => !names.Contains(property.Name, StringComparer.Ordinal)))
            throw new AppValidationException("The write payload contains an unsupported field.");
    }

    private static int Integer(JsonElement value, string name) => value.TryGetProperty(name, out var property) &&
        property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var result) ? result :
        throw new AppValidationException($"{name} must be an integer.");

    private static string? Text(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null) return null;
        return property.ValueKind == JsonValueKind.String ? property.GetString() : throw new AppValidationException($"{name} must be a string.");
    }

    private static void ValidateDuplicates(JsonElement value, int depth)
    {
        if (depth > 6) throw new AppValidationException("The write payload exceeds the supported nesting depth.");
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new AppValidationException("Duplicate write fields are not permitted.");
                ValidateDuplicates(property.Value, depth + 1);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            if (value.GetArrayLength() > 100) throw new AppValidationException("Write arrays are limited to 100 items.");
            foreach (var item in value.EnumerateArray()) ValidateDuplicates(item, depth + 1);
        }
    }
}
