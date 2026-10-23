# AI Smart Inventory Management System

Topic **MCP.2505 — Model Context Protocol, C# SDK**. This system connects a React/TypeScript interface to an ASP.NET Core API, PostgreSQL, and official C# MCP client/server SDKs. M2 and M3, the MCP read-tool path, and AI tool-calling orchestration are implemented. See [current verification and known gaps](docs/M3-VERIFICATION.md), [M2 verification](docs/M2-VERIFICATION.md), and [the original source audit](docs/M1-AUDIT.md).

The repository contains four .NET 10 projects, additive EF Core migrations, a React frontend, and official MCP SDK 2.3.0. A live upstream LLM call and extended business modules such as suppliers/orders are not claimed as complete. Preserve the existing database and migrations.

## Current layout

```text
AI-Smart-Inventory/
├── AI-Smart-Inventory.slnx
├── backend/
│   ├── SmartInventory.Api/
│   ├── SmartInventory.Application/
│   ├── SmartInventory.Infrastructure/
│   └── SmartInventory.McpServer/
├── .config/dotnet-tools.json
├── frontend/frontend/      # Connected React application; kept nested for the # path-safe Vite launcher
├── scripts/dev-db.py
├── docs/                    # System architecture and verification reports
└── ../backups/              # Workspace-level database archives; outside Git
```

## Development requirements

- .NET SDK 10, PostgreSQL and an existing development database named `ai_smart_inventory`.
- Python 3.10+ for the database helper; it uses the standard library only.
- PostgreSQL `psql`, `pg_dump` and `pg_restore` on `PATH`. The helper also checks `/Library/PostgreSQL/18/bin` on macOS. Use tools compatible with your PostgreSQL server; an older `pg_dump` cannot back up a newer server.
- NuGet access for the initial restore. The repository-local EF tool is pinned to **10.0.13**, matching the EF Core runtime packages.

Run all commands below from this directory. Preserve existing database contents and migrations.

## Configure PostgreSQL

The API loads `ConnectionStrings:DefaultConnection` through ASP.NET Core configuration. Keep credentials in Development user secrets or the `ConnectionStrings__DefaultConnection` environment variable. The API already declares its `UserSecretsId`. Do not commit passwords to tracked configuration.

An Npgsql connection string has this form; replace the placeholders in your private configuration:

```text
Host=localhost;Port=5432;Database=ai_smart_inventory;Username=<database-user>;Password=<database-password>
```

For Development, edit the API user-secrets JSON privately at `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json` on macOS/Linux:

```json
{
  "ConnectionStrings:DefaultConnection": "<your-private-Npgsql-connection-string>"
}
```

Use the `UserSecretsId` in `backend/SmartInventory.Api/SmartInventory.Api.csproj`. On Windows, the secrets directory is `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>`. The database helper reads this file only when `--use-user-secrets` is supplied and the environment is Development or unspecified. An explicit connection environment variable takes precedence.

## Restore, build and identify the DbContext

```sh
dotnet tool restore
dotnet restore AI-Smart-Inventory.slnx
dotnet build AI-Smart-Inventory.slnx --no-restore

ASPNETCORE_ENVIRONMENT=Development dotnet ef dbcontext info \
  --project backend/SmartInventory.Infrastructure \
  --startup-project backend/SmartInventory.Api \
  --context AppDbContext

ASPNETCORE_ENVIRONMENT=Development dotnet ef migrations list \
  --project backend/SmartInventory.Infrastructure \
  --startup-project backend/SmartInventory.Api \
  --context AppDbContext
```

The migrations and `AppDbContext` belong to Infrastructure; the API supplies the startup configuration and connection string. Selecting both projects avoids EF searching the wrong assembly for the context. For an environment connection string, the same commands work without user secrets.

## Inspect and back up before applying migrations

```sh
python3 scripts/dev-db.py --use-user-secrets inspect
python3 scripts/dev-db.py --use-user-secrets backup
```

`inspect` executes a read-only transaction. It reports table counts, applied migration IDs, duplicate SKU/usernames, orphan references and negative stock/prices. Exit code `0` means these checks passed; `2` means a required table/column is missing or data needs attention; `1` means configuration/tool/connection failure. These checks support migration review and do not replace business tests.

`backup` writes a PostgreSQL custom-format archive to `MCP_C#/backups/` (one directory above this repository) using an exclusive file and owner-only file permissions. An existing file is never overwritten. `AI_SMART_INVENTORY_BACKUP_DIR` can override the location; an absolute value is recommended, while relative values resolve from `MCP_C#/`. `--output` accepts a filename only, not a path, and the script prevents traversal outside the configured directory. Example: `backup --output before-migration.dump`. The helper passes credentials through the child process environment, never through command arguments or printed configuration. It accepts explicit single-host PostgreSQL URLs or basic Npgsql connection strings; unsupported options are rejected instead of silently changing connection behavior.

Generate an idempotent migration script for review, then apply pending migrations to the configured development database:

```sh
ASPNETCORE_ENVIRONMENT=Development dotnet ef migrations script --idempotent \
  --project backend/SmartInventory.Infrastructure \
  --startup-project backend/SmartInventory.Api \
  --context AppDbContext \
  --output ../backups/migration-review.sql

ASPNETCORE_ENVIRONMENT=Development dotnet ef database update \
  --project backend/SmartInventory.Infrastructure \
  --startup-project backend/SmartInventory.Api \
  --context AppDbContext

python3 scripts/dev-db.py --use-user-secrets inspect
```

The additive integrity migration can reject existing duplicates, orphan records or invalid negative values. Resolve identified data deliberately before applying it; do not delete existing migrations or reset the database.

## Restore a development archive

Choose an existing, empty development database as the connection target before restoring. The helper connects to that database and requires explicit write confirmation:

```sh
python3 scripts/dev-db.py --use-user-secrets restore \
  ../backups/<archive-name>.dump --confirm-restore
```

Restore runs in one transaction with `pg_restore --exit-on-error`. It never drops objects or creates a database. Conflicting existing objects cause a failure and rollback. A restore has not been performed as part of M1 verification.

## Run the current API

Run commands from the repository root. The API and MCP server both require `ConnectionStrings:DefaultConnection` and valid `Jwt:Issuer`, `Jwt:Audience`, and `Jwt:SigningKey` values in local .NET User Secrets or environment variables; never commit credentials.

```sh
dotnet run --project backend/SmartInventory.McpServer/SmartInventory.McpServer.csproj --launch-profile http
dotnet run --project backend/SmartInventory.Api/SmartInventory.Api.csproj --launch-profile http
```

The launch profiles use MCP `http://localhost:5206/mcp` and API `http://localhost:5062`. The MCP server provides authenticated Streamable HTTP tools and health endpoints. The API's C# MCP client connects to it on demand; MCP status performs real `ListToolsAsync` discovery and reports connected only when permitted inventory tools are present. Direct MCP tool testing does not require an LLM key.

For the AI Chatbot, configure `Llm:Provider=OpenAICompatible`, `Llm:BaseUrl`, `Llm:Model`, and `Llm:ApiKey` in the API project's .NET User Secrets or environment. Supported environment names are `Llm__Provider`, `Llm__BaseUrl`, `Llm__Model`, and `Llm__ApiKey`; for example, a local compatible endpoint can use `http://localhost:1234/v1` and the adapter calls `/chat/completions`. Remote endpoints must use HTTPS. Never put the key in appsettings, source or frontend config. Without a provider key the AI screen reports unavailable, while MCP Direct Test remains usable. No live provider call is claimed until a configured provider has been called successfully.

## Run the React frontend

Use Node.js 24 for the verified development setup:

```sh
cd frontend/frontend
npm ci
VITE_API_PROXY_TARGET=http://localhost:5062 npm run dev
```

Open `http://127.0.0.1:5173`. Log in using an Admin account provisioned for the development database. Products, Categories, Inventory, stock movement history, Profile, MCP Playground and AI conversations use the API. Admin can manage catalog items and adjust stock; Admin/Staff can receive or issue stock.

```sh
npm run build
```

The build includes TypeScript checks and emits `dist/`. Lazy-loaded pages and separate UI/React bundles keep the initial bundle manageable. The npm runner handles the existing workspace `#` path using a temporary symlink; this layout is intentionally retained because the launcher and browser-test scripts rely on it. See [frontend setup and token storage tradeoff](frontend/frontend/README.md).

## Troubleshooting

- **No DbContext was found:** run EF from the solution directory with Infrastructure as `--project`, API as `--startup-project`, and `--context AppDbContext`. Restore the local EF tool first.
- **EF tool/runtime mismatch:** use `dotnet tool restore` and the repository-local `dotnet ef`; avoid relying on a different global tool version.
- **Connection/authentication failure:** verify the configured existing database and PostgreSQL service, then rerun `inspect`. Do not paste passwords or complete connection strings into logs.
- **Integrity migration fails:** check the read-only preflight output and inspect the relevant data before retrying.
- **Frontend cannot reach API:** start the API and align `VITE_API_PROXY_TARGET` with its HTTP address (default project profile: port 5062). This variable is a public URL, not a place for secrets.
- **Vite fails resolving a `#` path:** use the repository's `npm run dev`/`npm run build` commands, which provide the temporary path alias.
- **MCP startup fails JWT validation:** configure JWT issuer, audience and signing key in local User Secrets/environment before starting either host.
- **AI unavailable:** configure backend-only LLM settings. A test-provider contract is not evidence that the external provider is reachable.
- **Backup location:** defaults to `../backups` relative to this repository; set `AI_SMART_INVENTORY_BACKUP_DIR` to choose another private workspace path.

## Validation and next milestones

Latest full-suite verification recorded in [M3-VERIFICATION.md](docs/M3-VERIFICATION.md): solution restore/build succeeded, 84 PostgreSQL-backed integration tests passed, React production build passed, and the browser smoke test verifies catalog, stock receipt/issue, movement history and MCP flows. Nine Python helper tests pass. Five Vietnamese intent paths are covered with a scripted provider and real MCP/PostgreSQL, but a real upstream model call and model language understanding remain unverified; see the report for other roadmap gaps.
