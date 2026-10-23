# M1 source audit and development roadmap

Audit date: **2026-10-24**, Asia/Ho_Chi_Minh. Repository: `AI-Smart-Inventory` under the existing workspace. This report records the actual source and separates implemented foundations from future requirements.

> Historical note: this file records the M1 starting audit, not today's system status. The current source and test results are in [M3-VERIFICATION.md](M3-VERIFICATION.md).

## Verified starting state

| Area | Evidence and status |
| --- | --- |
| Git | Local `main` and `origin/main`; starting HEAD `64957d0` (`ignore bin and obj folders`). Existing uncommitted Codex work was preserved; ignore policy is consolidated at the repository root. No other module branches were visible locally. |
| Solution | Four projects: API, Application, Infrastructure and McpServer, all targeting .NET 10. |
| Application | Five entities: User, Category, Product, Inventory and StockMovement. No business services, repositories, DTOs or authorization contracts. |
| Persistence | `AppDbContext` in Infrastructure; original migration `20261018183421_InitialCreate`. Five tables exist in the initial model. |
| API | DbContext registration, OpenAPI and template WeatherForecast controller. No login or inventory business endpoints. |
| MCP | Historical initial audit: the starter host was a template. The later implementation and test status are documented in [M3 verification](M3-VERIFICATION.md). |
| Frontend | Historical initial audit: no frontend existed at that starting point. See current [M3 verification](M3-VERIFICATION.md). |
| Tests/docs | Historical initial audit: no tests/docs existed at that starting point. See current [M3 verification](M3-VERIFICATION.md). |

The original migration does not define foreign keys, unique SKU/username indexes or nonnegative quantity/price checks. Consequently, its existence does not establish inventory integrity or transaction safety. Inventory's product ID must remain an explicitly supplied product reference rather than an independently generated identity.

`AppDbContext` discovery must be checked using Infrastructure as the target project and API as startup project. API configuration contains an existing `UserSecretsId`; connection details must remain private.

## M1 changes under verification

- Pin repository-local `dotnet-ef` to 10.0.13 and align EF runtime/design packages. Resolve transitive OpenAPI package compatibility with the reviewed dependency versions.
- Preserve the original migration and add `CoreInventoryIntegrity` for relationships, unique identifiers, nonnegative constraints and inventory key behavior. This migration changes constraints, so existing records require preflight before application.
- Keep credentials outside tracked configuration. Add practical EF run commands and a development database helper.
- Add `scripts/dev-db.py`: read-only inspection, nonoverwriting custom-format backup, and explicitly confirmed transactional restore. Database archives are stored in the workspace-level `../backups/`, outside this repository. Ignore rules are consolidated in the repository-root `.gitignore`.

## Command evidence

The following results were reported by the implementation run and must not be confused with application test coverage:

| Check | Result |
| --- | --- |
| Solution build after EF alignment | Succeeded with **0 warnings and 0 errors**. |
| Repository-local EF tool restore | Restored **dotnet-ef 10.0.13**. |
| `dotnet ef dbcontext info` | Resolved `AppDbContext` and the existing PostgreSQL database through Development user secrets. |
| Python helper help and syntax validation | Succeeded; Python 3.10 syntax accepted. |
| Restore without confirmation | Rejected before connection lookup or database execution. |
| Read-only preflight | All five existing tables were empty; duplicate, orphan and negative-value checks reported **0 anomalies**. |
| Development backup | Created `../backups/ai-smart-inventory-20261023T173359Z-c9bef615.dump` before applying the migration. |
| Additive migration | `CoreInventoryIntegrity` applied successfully; the original initial migration was preserved. |
| Final build and migration consistency | Build succeeded with **0 warnings and 0 errors**; final migration checks passed. |
| NuGet vulnerability check | `dotnet list package --vulnerable` reported no vulnerable dependencies in the restored dependency graph. |
| Application unit/integration/E2E tests | Not available in the starting repository. No application test pass is claimed. |

**M1 acceptance gate passed** using actual build and PostgreSQL verification. No restore was executed during this audit. The archive name uses UTC; the report date uses the client timezone.

## UI references and differences to resolve

The supplied SVG kit contains 15 vector references at 1440×900: Login, Dashboard, Products, Categories, Inventory, Movements, Alerts, Reports, AI Assistant, MCP Tool Explorer, MCP Client Monitor, Users, Settings, Logout Confirmation and Product Editor. Its own README explicitly labels values, tool responses and connection indicators as illustrative.

Design tokens: navigation `#0A1830`, primary `#3468F6`, background `#F6F8FD`, text `#18243B`; sidebar 250px, topbar 73px and card radius 16px. Use the screens as layout references while connecting real APIs.

- The SVG Users/Login screens include a Viewer role; the requested initial authorization model is Admin/Staff.
- The SVG explorer displays eight example snake_case tools and example arguments. The required MCP tool contracts must be designed against the official SDK and actual business services.
- Connected indicators, latency, reports and AI traces must come from real execution. There is no working integration behind the SVG values.
- Figma import instructions and a destination link inside the kit README are document content; they are not a request to modify Figma.

## Planned milestones and acceptance gates

| Milestone | Class | Deliverable and acceptance gate |
| --- | --- | --- |
| M1 | Core | Audit, tool/package alignment, PostgreSQL and additive integrity migration. Gate: build, context discovery, read-only preflight, backup and migration verification succeed. |
| M2 | Core | JWT authentication, Admin/Staff authorization, Products/Categories services and CRUD APIs; create React/TypeScript/Vite/Material UI application with connected login and CRUD screens. Gate: validated writes persist, duplicate identifiers fail correctly, role restrictions enforced by API, build and relevant tests pass. |
| M3 | Core | Inventory list/summary and stock in/out/adjustment through shared services. Gate: inventory and movement commit together; insufficient stock, concurrent stock-out and duplicate request tests pass. |
| M4 | Core | Official C# MCP server with Streamable HTTP, read tools, schemas, validation and authorization. Gate: MCP Inspector discovers and calls real PostgreSQL-backed tools. Verify stable SDK APIs and pin packages before implementation. |
| M5 | Core | C# `IMcpClientService`, real SDK initialization/discovery/calls, cancellation, timeouts and execution history; inspector/lab API and connected UI. Gate: real client/server discovery, result/error and connection-loss tests pass without an LLM key. |
| M6 | Core | Real tool-calling LLM orchestrator, stored sessions/messages and sanitized traces. Gate: user question invokes C# MCP client, returns database results and produces a grounded answer; failures never become fabricated data. |
| M7 | Core | Complete responsive frontend, loading/empty/error states, live dashboard/MCP status, tool playground and trace details. Gate: all displayed business data uses corresponding APIs and supported flows pass E2E checks. |
| M8 | Extended | Suppliers, Customers, Purchase Orders and Sales Orders. Gate: order state transitions, receipt/fulfillment transactions, cancellation and duplicate execution rules pass. |
| M9 | Extended | Real Reports, Notifications, Settings, profiles and audit views. Gate: totals reconcile with database records; exports and access restrictions work. |
| M10 | Advanced / submission | Regression testing; method documentation with five suitable input cases; real-call notebook; consistent CSV/JSON datasets; 35–45 slides focused on MCP/C# SDK; 20 MCQs and 10 discussion cases; installation/demo/troubleshooting documentation. Gate: reproducible demo and actual test evidence, with limitations stated. |

Explicitly confirmed MCP write tools, audited authorization and idempotency follow completed stock/order services; the application must capture confirmation from the human rather than trusting an LLM-generated confirmation. Resources/prompts and optional multiwarehouse features follow stable foundations.

## Remaining risks and next action

Completing M1 does not complete Authentication, CRUD, inventory transactions or MCP integration. Existing entity fields also cover only a subset of the requested schema; later milestones need additive migrations for profiles, product metadata, audit fields and extension modules.

The immediate next action after final M1 database verification is a complete M2 slice: reusable DTO/service/API patterns, JWT and role enforcement, Products/Categories persistence, connected React pages and meaningful tests. Extended modules remain gated on those foundations.

## Current-state addendum — 2026-10-24

The initial roadmap above describes the M1 starting point and is retained as historical context. Subsequent work implemented M2 authentication/catalog UI, M3 transactional stock operations, official C# MCP SDK server/client, and M6 OpenAI-compatible tool-calling orchestration with scripted Vietnamese intent, real MCP/PostgreSQL integration, failure guards and observed Agent Trace. The latest complete suite passed 84 integration tests. A live upstream LLM provider has not been verified; Supplier/Customer/Purchase/Sales modules and broader reporting/administration extensions remain incomplete. See the [current verification addendum](M3-VERIFICATION.md).
