using System.ComponentModel;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Mcp;

namespace SmartInventory.McpServer.Knowledge;

/// <summary>Public business documentation, separate from live-data tools and provider-generated answers.</summary>
[McpServerResourceType]
public sealed class InventoryResources
{
    [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff)]
    [McpServerResource(UriTemplate = McpKnowledgePolicy.StockPolicyUri, Name = "StockPolicy", MimeType = "text/markdown")]
    [Description("Inventory transaction, idempotency, quantity and reporting rules. Contains no configuration secrets.")]
    public static string StockPolicy() => """
        # Inventory business policy
        Quantities are nonnegative integers. Stock receipts, issues and adjustments use the shared stock service.
        Every successful change and its StockMovement commit in one PostgreSQL transaction.
        Product/inventory locks prevent concurrent overselling. A request UUID binds to its original payload;
        replay returns the original result and a different payload conflicts. Never retry a write with a new UUID.
        Purchase receipts increase stock. Sales fulfillment decreases stock; creating/confirming an order does not.
        Cancellation is allowed only before any receipt or fulfillment; cancellation never reverses stock.
        Low stock means quantity < product.minStock, or < an explicit nonnegative tool threshold.
        Inventory cost value uses costPrice; retail valuation uses price. Neither is sales revenue.
        Period sales value uses actual fulfillment timestamps; received cost uses receipt timestamps.
        All stored timestamps are UTC. Read scope omitted means aggregate inventory; explicit warehouseId selects one warehouse.
        Write warehouseId omitted means MAIN. Per-warehouse low-stock checks reuse product.minStock, not separate thresholds.
        Movement quantityBefore/After are global aggregates; warehouseQuantityBefore/After describe local balances when known.
        Transfers conserve aggregate stock and never emit temporary global low-stock alerts.
        MCP exposes authorized reads and five explicitly confirmed writes; transfers use the REST workflow.
        """;

    [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff)]
    [McpServerResource(UriTemplate = McpKnowledgePolicy.ArchitectureUri, Name = "SystemArchitecture", MimeType = "text/markdown")]
    [Description("Explains the real C# MCP client/server and optional LLM request path.")]
    public static string Architecture() => """
        # AI Smart Inventory architecture
        React -> ASP.NET Core API -> business services -> EF Core/Npgsql -> PostgreSQL.
        AI: React -> Chat API -> ILlmProvider -> discovered tool selection -> official C# MCP Client
        -> Streamable HTTP -> official C# MCP Server -> shared business services -> PostgreSQL
        -> tool result -> provider -> assistant response.
        Direct Test Mode uses the same C# MCP client and server without contacting an LLM.
        Tools perform live business queries. Resources provide documentation. Prompts return reusable message
        templates and do not call an LLM, execute tools, or prove that an AI answer was generated.
        Each client operation owns and disposes its SDK transport/client, bounds its timeout, and propagates
        the authenticated caller. Audit records and trace responses never contain provider keys or JWT secrets.
        """;
}

[McpServerPromptType]
public sealed class InventoryPrompts
{
    [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff)]
    [McpServerPrompt(Name = McpKnowledgePolicy.AnalyzeLowStock)]
    [Description("Prepare a tool-grounded low-stock analysis request. Returns a template, not an AI answer.")]
    public static PromptMessage AnalyzeLowStock(
        [Description("Planning horizon in days, an integer string from 1 through 90; defaults to 14.")] string horizonDays = "14")
    {
        if (!int.TryParse(horizonDays, NumberStyles.None, CultureInfo.InvariantCulture, out var days) || days is < 1 or > 90)
            throw new McpException("horizonDays must be an integer string between 1 and 90.");
        return new PromptMessage
        {
            Role = Role.User,
            Content = new TextContentBlock { Text = $"Phân tích hàng tồn thấp cho kế hoạch {days} ngày. " +
                "Gọi GetLowStockProducts và GetInventorySummary để lấy dữ liệu thực; xử lý phân trang. " +
                "Phân biệt minStock với dự báo nhu cầu. Nếu chưa có dữ liệu bán đủ dài, nêu rõ giới hạn " +
                "và không bịa số lượng cần mua. Trích SKU, tồn hiện tại và dữ liệu công cụ đã dùng." }
        };
    }

    [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff)]
    [McpServerPrompt(Name = McpKnowledgePolicy.InventoryReport)]
    [Description("Prepare an inventory report request using current tools; no provider or database query runs here.")]
    public static PromptMessage InventoryReport() => new()
    {
        Role = Role.User,
        Content = new TextContentBlock { Text = "Lập báo cáo kho bằng GetInventorySummary, GetCategorySummary và " +
            "GetStockMovementHistory. Nêu thời điểm quan sát UTC, phân biệt giá vốn tồn kho, giá trị bán lẻ " +
            "và doanh thu thực hiện. Không thay kết quả công cụ lỗi bằng số liệu tự tạo." }
    };
}
