# MCP C# implementation

The API and MCP server use the official C# SDK **2.3.0**, pinned in their project files. The API references `ModelContextProtocol.Core`; the HTTP server references `ModelContextProtocol.AspNetCore`. The official release was published on 20 October 2026. No TypeScript MCP client is involved.

## Runtime path

```text
React -> authenticated ASP.NET API -> official C# McpClient
      -> Streamable HTTP /mcp -> official C# MCP server
      -> shared business query services -> EF Core -> PostgreSQL
```

The API starts without connecting to MCP. Each status, discovery or execution operation creates a client and transport scoped to the caller, carries that caller's bearer token, and disposes both resources. A server outage therefore does not prevent login, product/category operations or API liveness checks.

The MCP server uses `HttpServerSessionMode.Stateless`. SDK 2.x negotiates the current protocol through `server/discover`; `initialize` remains a compatibility path for older protocol revisions. A modern stateless connection does not create an `Mcp-Session-Id`. `McpClient.CreateAsync` performs the appropriate negotiation; application code does not handcraft initialization messages.

## Start locally

Configure the API's existing user-secrets profile with the PostgreSQL connection and JWT signing key. The MCP project's development `UserSecretsId` deliberately matches the API's, so both first-party hosts read the same local configuration. Environment variables override configuration when running the compiled applications.

```bash
dotnet run --project backend/SmartInventory.McpServer --launch-profile http
dotnet run --project backend/SmartInventory.Api --launch-profile http
```

The default MCP endpoint is `http://localhost:5206/mcp`; the API is `http://localhost:5062`. Both hosts expose `/health/live`. API `/health/ready` checks PostgreSQL connectivity and pending migrations. MCP health is independent of a database query.

Non-secret API configuration:

```json
{
  "Mcp": {
    "ServerUrl": "http://localhost:5206/mcp",
    "TimeoutSeconds": 30,
    "UseSharedFirstPartyAudience": true
  }
}
```

`ServerUrl` comes from server configuration, never browser input. HTTPS is accepted; plain HTTP is limited to loopback endpoints. Credentials, query strings and fragments are rejected. Redirects are disabled so credentials cannot follow a redirect to another resource. The timeout must be between 5 and 60 seconds and bounds negotiation, discovery and tool execution together. There are no automatic retries.

`UseSharedFirstPartyAudience` is an explicit architecture choice: these two hosts represent the same first-party inventory resource and validate the same configured issuer, audience and signing key. Every request also checks the current database user, role, active state and token version. An API-only token for a different audience must not be forwarded to another MCP resource. Integrating third-party MCP resources requires their own audience and authorization flow.

The server accepts loopback Host headers and validates any supplied Origin. MCP Inspector origins default to `http://localhost:6274` and `http://127.0.0.1:6274`; configure `Mcp:AllowedOrigins` when a different trusted Inspector origin is required. Requests without Origin remain available to the C# client. Both endpoint authorization and tool authorization require Admin or Staff. Tool annotations describe behavior and do not grant permission.

## SDK methods

| Method | Inputs | Return | Behavior |
| --- | --- | --- | --- |
| `McpClient.CreateAsync` | `IClientTransport`, optional `McpClientOptions`, logger factory, cancellation token | `Task<McpClient>` | Connects and negotiates server capabilities. |
| `ListToolsAsync` | optional request options and cancellation token | `ValueTask<IList<McpClientTool>>` | Discovers tools and schemas using the protocol, including pagination. |
| `CallToolAsync` | tool name, optional `IReadOnlyDictionary<string, object?>` arguments, progress, request options, cancellation token | `ValueTask<CallToolResult>` | Invokes a discovered permitted tool. |
| `DisposeAsync` | none | `ValueTask` | Releases the client session or the outer transport's owned HTTP client. Dispose both client and outer transport. |

`CallToolResult.Content` contains typed content blocks. `StructuredContent` is `JsonElement?`; `IsError` is `bool?`, so check `is true`. A normal tool failure may be returned without an exception. `McpProtocolException` distinguishes protocol errors from tool results. Cancellation raises `OperationCanceledException`; HTTP transport failures can raise `HttpRequestException`. The application converts these cases into separate recorded statuses.

`ServerInfo` is nullable in SDK 2.3.0 and is used only for display. Tool discovery returns `McpClientTool`, which also derives from the official `Microsoft.Extensions.AI.AIFunction`. The application exposes neutral JSON schemas through `IMcpClientService`, so its AI layer can use discovered tool definitions without making the browser an MCP client.

Example of the SDK calls used by the application:

```csharp
using System.Net.Http;
using ModelContextProtocol.Client;

using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
deadline.CancelAfter(TimeSpan.FromSeconds(30));
using var httpClient = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
{
    Timeout = Timeout.InfiniteTimeSpan
};
await using var transport = new HttpClientTransport(new HttpClientTransportOptions
{
    Endpoint = configuredTrustedEndpoint,
    TransportMode = HttpTransportMode.StreamableHttp,
    EnableStandaloneGetStream = false,
    AdditionalHeaders = new Dictionary<string, string>
    {
        ["Authorization"] = "Bearer " + callerAccessToken
    }
}, httpClient);
await using var client = await McpClient.CreateAsync(transport,
    new McpClientOptions { InitializationTimeout = TimeSpan.FromSeconds(30) },
    cancellationToken: deadline.Token);
var tools = await client.ListToolsAsync(cancellationToken: deadline.Token);
var result = await client.CallToolAsync("GetLowStockProducts",
    new Dictionary<string, object?> { ["threshold"] = 10 },
    cancellationToken: deadline.Token);
```

## Available read tools

Products are active products unless the tool describes historical movements. Page numbers start at 1 and page size is limited to 100. Queries use PostgreSQL data and bounded, asynchronous EF Core queries.

| Tool | Parameters | Result |
| --- | --- | --- |
| `GetAllProducts` | `page=1`, `pageSize=20` | Paged products with category and current quantity. |
| `GetProductById` | positive `id` | One active product, or a tool error. |
| `GetProductBySku` | nonempty `sku`, maximum 80 characters | One active product; ambiguous legacy case variants produce an error. |
| `SearchProducts` | nonempty `search`, maximum 200 characters; page options | Products matching SKU or name. |
| `GetProductsByCategory` | positive `categoryId`; page options | Products in that category. |
| `GetLowStockProducts` | optional nonnegative `threshold`; page options | `quantity < threshold`, or `quantity < min_stock` when omitted. Explicit 0 returns no products under the nonnegative stock constraint. |
| `GetOutOfStockProducts` | page options | Products whose quantity is 0. |
| `GetInventorySummary` | none | Product/category counts, quantity, cost value, retail value, stock alerts and actual movement count. These values are not sales revenue. |
| `GetStockMovementHistory` | optional positive `productId`; page options | Actual movement records, including historical products. Missing before/after quantities are not invented. |
| `GetCategorySummary` | none | Category counts, quantities, cost value and retail value. |

The shared policy rejects unknown tools, unknown or duplicate parameter names, nested parameter values and arguments larger than 16 KiB. Business methods validate ranges and required values explicitly: the SDK uses data annotations for schema descriptions but does not enforce them as runtime business validation.

Tool methods are registered through `WithTools<InventoryTools>()`, return typed structured data, and resolve business services from the current request scope. The SDK automatically injects services, `ClaimsPrincipal` and `CancellationToken` without adding these parameters to the input schema. `AddAuthorizationFilters()` enforces the tool authorization attributes.

Supplier/order tools, write tools, resources and prompts are not advertised as implemented. Those require their actual business modules and, for writes, the application confirmation/idempotency flow.

## API and persisted trace

All routes below require an active Admin or Staff user:

| Route | Response |
| --- | --- |
| `GET /api/mcp/status` | A fresh SDK connection and `ListToolsAsync` discovery probe with `connected`, transport, configured URL, optional server metadata/error and `checkedAt`. `connected` is false if the server is reachable but no allowlisted inventory tool is advertised. |
| `GET /api/mcp/tools` | Discovered allowlisted tool names, descriptions, input schemas and annotations. |
| `POST /api/mcp/tools/{toolName}/execute` | Body `{ "arguments": {} }`; returns the persisted execution record. |
| `GET /api/mcp/executions?page=1&pageSize=20` | Paged history owned by the signed-in user. |
| `GET /api/mcp/executions/{id}` | One owned record; another user's ID returns 404. |

Execution records include an integer ID, tool name, sanitized input/output, status, duration, UTC timestamp and optional safe error. Output preserves the protocol result shape, including `content`, `structuredContent` and `isError`. Structured values follow the advertised output schema; clients do not assume a `result` wrapper.

The server audits actual tool calls, including Inspector calls, and returns its record ID in namespaced result metadata. The API only reads that record under the current user's ownership. Protocol/transport failures that cannot return a server audit ID are recorded by the C# client. A database outage can prevent persistence; the application does not invent a stored execution ID.

Statuses distinguish `Succeeded`, `ToolError`, `ValidationError`, `ProtocolError`, `TransportError`, `Timeout` and `Cancelled`. Valid execute requests return an execution result even when its `isError` is true. Malformed HTTP JSON returns ProblemDetails 400. An unavailable tools endpoint returns ProblemDetails 503; status probing returns `connected: false`. Trace durations measure actual execution; the UI does not fabricate database or AI spans.

Sensitive JSON field names are redacted, bearer strings are masked, and audit payloads are bounded. JWTs are attached only to the operation's transport headers, never shared through singleton default headers or printed in execution logs.

## Official references

- [SDK 2.3.0 release](https://github.com/modelcontextprotocol/csharp-sdk/releases/tag/v2.3.0)
- [SDK packages and source](https://github.com/modelcontextprotocol/csharp-sdk)
- [HTTP transport and host/origin guidance](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/transports/transports.html)
- [Stateless and stateful behavior](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/stateless/stateless.html)
- [Tool registration, schemas and structured content](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/tools/tools.html)
- [Identity and authorization filters](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/identity/identity.html)
- [MCP request filters](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/filters.html)
- [Client API signatures](https://csharp.sdk.modelcontextprotocol.io/v2/api/ModelContextProtocol.Client.McpClient.html)
- [Pinned source for tool result types](https://github.com/modelcontextprotocol/csharp-sdk/blob/v2.3.0/src/ModelContextProtocol.Core/Protocol/CallToolResult.cs)
