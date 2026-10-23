# M2 — Authentication, Products and Categories

Verified on **2026-10-24 (Asia/Ho_Chi_Minh)** as the M2 milestone report. Its remaining-issues section is a historical snapshot, not the current project status; see [M3 verification](M3-VERIFICATION.md) for the later M3, MCP, AI, integration-test and frontend work.

## A. Work completed

JWT login, profile editing, password changes and logout now use PostgreSQL users and ASP.NET Core PasswordHasher. Each authenticated request checks the current account status, role and token version. Password changes and logout revoke previously issued tokens. Admin can modify the catalog; Staff can read it. Administrator bootstrap is an explicit Development command with credentials from private configuration, without a default password.

Products and Categories have reusable application contracts, EF-backed services, REST controllers and connected React forms. Product list supports server pagination, search, category filters, sorting and inactive records. New products receive a zero-quantity inventory row in the same transaction. Deletion deactivates a product; category deletion is blocked while products reference it.

CSV imports validate all rows before committing and preserve inventory/status when updating products. Exported text is escaped for spreadsheet use and can be imported again using the format marker. Image upload supports PNG/JPG/WebP, bounds size, checks signatures and generates storage names. The UI includes loading, empty and error states, confirmation dialogs, details, CSV operations, image upload and session expiration.

## B. Files added

- `backend/SmartInventory.Application/Authentication`, `Catalog`, `Common`: DTOs, interfaces and application errors.
- `backend/SmartInventory.Infrastructure/Authentication`, `Catalog`: authentication, bootstrap, catalog, CSV, image and seed services.
- `backend/SmartInventory.Api/Authentication`, authentication/catalog controllers and `ApiExceptionHandler.cs`.
- `20261023174640_AuthenticationAndProductCatalog` migration and designer.
- `frontend/frontend`: React/TypeScript/Vite/MUI app, exact dependency pins, lock file, proxy and portable Vite launcher.
- `tests/SmartInventory.IntegrationTests`, `tests/browser`, `tests/test_dev_db.py`, `scripts/browser-smoke.py`.

Some shared authentication files may subsequently move to Infrastructure when both API and MCP hosts need them; the behavior remains covered by regression tests.

## C. Files modified

The solution now includes tests. User/Product/Category entities and DbContext have additive metadata fields and defaults. API startup registers services, validation, structured logging, constrained CORS, rate limits, authentication, error handling and health probes. Package references/locks and README instructions were updated. The initial migration and the user's pre-existing `.gitignore` edit were preserved.

## D. Architecture and logic

`React → authenticated REST controller → application service interface → EF service → PostgreSQL`.

Controllers do not implement inventory adjustments. The zero-stock catalog seed has no fabricated movement history. Thirty-two explicitly labelled development products and five categories were added idempotently to the development database. Their prices are sample data, not live market prices or revenue.

Frontend tokens stay in memory and sessionStorage for tab reloads, never localStorage. This development implementation requires protecting the page from script injection; a production deployment can use a BFF/HttpOnly-cookie design. No credentials are committed. Product images are served publicly from generated paths; authentication protects upload operations.

## E. Commands executed

```sh
dotnet build AI-Smart-Inventory.slnx --no-restore --disable-build-servers
dotnet test tests/SmartInventory.IntegrationTests/SmartInventory.IntegrationTests.csproj \
  --no-restore --disable-build-servers --logger trx \
  --results-directory tests/SmartInventory.IntegrationTests/TestResults
python3 -m unittest discover -s tests -p 'test_*.py' -v
python3 scripts/browser-smoke.py
python3 scripts/dev-db.py --use-user-secrets backup
dotnet ef database update --no-build \
  --project backend/SmartInventory.Infrastructure \
  --startup-project backend/SmartInventory.Api --context AppDbContext \
  -- --environment Development
ASPNETCORE_ENVIRONMENT=Development DOTNET_ENVIRONMENT=Development \
  dotnet run --no-build --no-launch-profile --project backend/SmartInventory.Api -- --seed
# In frontend/frontend:
npm run build
```

.NET commands needed execution outside the restricted sandbox because sandboxed MSBuild processes stalled. Database tests also need localhost access. The development environment flag is required for seed/bootstrap commands.

## F. Actual results

| Check | Observed result |
| --- | --- |
| Solution build | Passed, **0 errors / 0 warnings**. |
| xUnit suite | **27 passed, 0 failed, 0 skipped**: 21 API/PostgreSQL cases and six culture/boundary cases. |
| Python database-helper tests | **6 passed**; synthetic parser/redaction/configuration tests, no database simulation presented as integration evidence. |
| Frontend production build | Passed without warnings after resolving the existing `#` path issue. |
| Real Chrome Playwright flow | **1 passed**, 5.9 seconds for the test; login → category creation → product creation → search → edit → deactivate → mobile navigation → logout. |
| Public database migration | Additive M2 migration applied successfully after backup. |
| Development seed | Created **32 products** with **zero stock**. |

The PostgreSQL integration suite creates a fresh `inventory_test_<UUID>` schema per run and drops only that generated schema. Browser testing uses a separate `inventory_browser_<UUID>` schema, temporary credentials and temporary API/frontend ports; these are cleaned afterward. Existing public business data is not changed by the tests.

TRX evidence is in the ignored test results directory. Browser screenshots are in `tests/browser/test-results/`: `products-desktop.png` and `categories-mobile.png`. These screenshots show real test data created through the UI.

The initial test runs found and fixed two runtime defects that compilation alone did not catch: ASP.NET positional-record validation metadata and decimal RangeAttribute parsing under `vi-VN`. Browser QA also found Vite's own client module resolving incorrectly through the workspace name `MCP_C#`; the launcher uses a temporary clean symlink and preserves module symlinks without moving the repository.

## G. Remaining issues

- Stock-in/out/adjustment, concurrency and movement history writing belong to M3 and are not enabled by M2.
- Catalog export currently implements CSV. Dedicated Excel export remains tracked, as do reports/PDF exports.
- Full user administration, audit trail and application preferences remain on the roadmap.
- A live LLM provider has not been configured or called at this milestone.

## H. Next step

The user's updated priority is the official C# MCP Server → C# MCP Client → PostgreSQL path, with MCP Direct Test Mode independent of an LLM key. The read-tool slice comes next; stock/order write tools remain gated on transaction-safe business operations and explicit human confirmation.
