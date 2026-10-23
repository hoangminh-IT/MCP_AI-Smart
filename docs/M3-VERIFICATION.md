# M3 — Inventory, MCP and AI integration verification

Audit and verification performed **2026-10-24** against the existing `main` worktree. No repository reset, commit or push was performed. This is an observed status report, not a claim that every planned extended module is complete.

## Verified implementation

- Inventory receipt, issue and adjustment operations use the existing `IStockService`/`StockService`. Quantity and movement are committed in one PostgreSQL transaction; products/inventory are locked consistently, stock cannot go negative, and each request is protected by a globally unique idempotency key with payload/user replay checks.
- Inventory summary, filtered stock list and movement history REST endpoints are implemented. Admin-only adjustment and Admin/Staff receipt/issue policies are enforced at the API.
- MCP server uses official `ModelContextProtocol.AspNetCore` **2.3.0**, Streamable HTTP at `/mcp`, stateless sessions, authenticated tools, and real PostgreSQL-backed read-only inventory tools. The API uses official `ModelContextProtocol.Core` **2.3.0**; its C# client performs `McpClient.CreateAsync`, `ListToolsAsync` and `CallToolAsync` against the configured server. MCP Direct Test does not require an LLM key.
- The API's MCP status probe now performs real `ListToolsAsync` discovery before it reports connected; a reachable server with no permitted inventory tools is reported as not connected/usable.
- AI persistence and orchestration are implemented. The provider adapter is OpenAI Chat Completions-compatible and performs function/tool calling by passing tools discovered from MCP; every selected tool is validated and invoked through the C# MCP client. User Secrets/environment configuration is used for provider credentials. No live provider/key was available for this verification, so no real LLM request is claimed.
- React now has an inventory summary/list with receipt, issue and Admin adjustment forms, stable per-request idempotency keys, plus a paginated movement history view. MCP lab/playground/trace and AI session UI already exist.

## Defect found and repaired

The first current integration-test run failed before test bodies because EF Core reported `PendingModelChangesWarning`. The runtime `AppDbContext` included inventory update time and M3 stock movement fields/indexes/FK/check constraints, but the migration history and snapshot did not contain them. Added the additive `StockOperationTransactions` migration and aligned the snapshot. Existing migrations were not rewritten. The integration suite then passed, and `dotnet ef migrations has-pending-model-changes` reported no changes.

## Commands and observed results

| Verification | Observed result |
| --- | --- |
| `dotnet restore AI-Smart-Inventory.slnx --disable-build-servers` | Succeeded. |
| `dotnet build AI-Smart-Inventory.slnx --no-restore --disable-build-servers` before warning cleanup | Succeeded; 4 xUnit analyzer warnings in stock-test assertions. Those assertions were changed to analyzer-preferred `Assert.Single` predicate overloads without changing test meaning. |
| `dotnet test tests/SmartInventory.IntegrationTests/SmartInventory.IntegrationTests.csproj --no-restore --disable-build-servers` | **84 passed, 0 failed, 0 skipped** on the latest complete M6 run. Uses isolated PostgreSQL schemas and a real loopback MCP server process. Includes M3 transactions/concurrency/idempotency; MCP SDK discovery; all four Direct Test tools against real PostgreSQL test rows; and AI provider/orchestration contracts. |
| `cd frontend/frontend && npm run build` | Succeeded; TypeScript and Vite production build passed after adding inventory/movement screens. |
| `python3 scripts/browser-smoke.py` | **1 Playwright test passed**: login, category/product CRUD, search, stock receipt, stock issue, persisted stock quantity and movement-history UI, MCP lab/playground/trace, AI unconfigured state, responsive navigation and logout. Uses real API/PostgreSQL through a disposable schema. |
| `python3 -m unittest discover -s tests -p 'test_*.py' -v` | **9 passed**, including workspace default, environment override, and output-path safety checks. These use temporary/synthetic paths and do not contact PostgreSQL or write a database archive. |
| Read-only PostgreSQL inspection | 5 migrations, 5 categories, 32 products, 32 inventory rows, 0 users and 0 stock movements; no duplicate keys, orphan rows or negative quantities/prices. |
| Database backup | Created a new non-overwriting custom-format archive before migration review. |
| MCP standalone startup and endpoint probe | First attempt correctly failed fast because Jwt issuer/audience/signing key were not configured. A second localhost-only startup with a temporary verification key listened on `http://localhost:5206`; `/health/live` and `/health` returned HTTP 200 Healthy. Anonymous POST to `/mcp` returned HTTP 401. Process was stopped afterward. |
| MCP SDK behavior | Integration tests verified API client connection/status, tool listing and calls to a separate running real C# MCP server over Streamable HTTP; tool results include data written through REST to the isolated PostgreSQL test schema. |
| Live LLM provider | **Not verified.** Status returns unconfigured without an API key; API key remains private and must be set by the developer in .NET User Secrets before an actual provider smoke test. |

## M6 AI Chatbot follow-up — 2026-10-24

- `AiChatService` continues to call only `IMcpClientService` for tool calls. The MCP client opens the official C# SDK transport; AI orchestration has no direct REST or database fallback. The provider receives the authenticated user's Vietnamese question plus the schemas returned by MCP `ListToolsAsync`; it may select an allowlisted read tool and validated scalar arguments.
- The system prompt now explicitly guides intent-to-tool routing for low-stock thresholds, catalog search, summary/valuation, and recent movement questions; it distinguishes cost valuation from retail inventory valuation and warns that neither is sales revenue. Model interpretation still requires a live-provider run to validate.
- MCP Agent Trace now returns and displays each actual tool name, sanitized input JSON, protocol output JSON, server/client execution status, duration and execution ID. Discovery and LLM steps record elapsed time and failure status too. Only MCP results with `isError=false` and canonical `Succeeded` status can be used for a successful AI answer. The first MCP tool error aborts the batch; a final LLM answer is not persisted or returned successfully from incomplete tool data. Failures remain persisted as a failed assistant record with the observed trace; tool rounds and tool count stay bounded.
- Configuration is optional for startup and private to the API host: `Llm__Provider=OpenAICompatible`, `Llm__BaseUrl`, `Llm__Model`, `Llm__ApiKey` environment values or their `Llm:*` .NET User Secrets equivalents. HTTPS is required remotely; HTTP is allowed for loopback. No key was configured or printed during tests.
- New scripted integration coverage maps the five requested Vietnamese prompts to expected tool contracts, then uses real C# MCP client/server and isolated PostgreSQL test rows to verify the selected tool, parameters, output, status and duration. This is an explicitly scripted intent contract; it does **not** prove a live model can interpret those questions.
- Scripted tests also verify failed MCP tools cannot lead to an invented assistant answer, that the configured tool-round budget is enforced, and that provider timeout is traced and releases the conversation lease. Provider contract tests cover a Vietnamese prompt, MCP-discovered schema serialization/function selection, invalid settings, missing key, network/HTTP errors and secret redaction.
- **Live LLM provider remains unverified** until the developer supplies a valid API key, model and compatible endpoint in local User Secrets/environment and a real end-to-end request succeeds. MCP Direct Test remains available without those settings.

The earlier attempted `dotnet run` from the solution directory exited with code 1 because there is no runnable project at that path. ASP.NET Core projects must be run with `dotnet run --project <path-to-csproj>`; the MCP standalone check above used this form.

The development database inspection and EF commands used existing local user secrets. The migration is additive, but this verification's `database update` reported the configured database was already current. `migrations list` and a subsequent read-only inspect showed the migration history; no destructive database operation was run. The actual server/tool round-trip uses isolated test schemas, which are removed by the test fixture.

## Current scope and remaining work

M3 backend transaction behavior and the official C# MCP read path have executable verification, and the browser suite verifies stock receipt/issue, movement history and MCP Playground. Direct Test tools are API -> official SDK client -> real MCP process -> EF/PostgreSQL; they are not canned results. M6 is verified with 84 full-suite tests and focused real-MCP/PostgreSQL scripted-provider flows, including all five example question intents, observed trace input/output, error/no-fabrication, round budget, and provider timeout. A real LLM request remains blocked on a developer-provided provider key/model/endpoint; scripted tests do not prove that a live model interprets Vietnamese questions or that any particular provider endpoint is reachable. Products/Categories/Profile, stock, MCP screens and AI orchestration are implemented; Supplier/Customer/Purchase/Sales modules, user administration, dashboard/reporting/notifications/settings, Excel/PDF exports and MCP write tools with an explicit human confirmation flow are not evidenced as complete here. Do not present these roadmap modules as finished.
