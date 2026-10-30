# AI Smart Inventory Management System

**MCP.2505 — Model Context Protocol, C# SDK.** Ứng dụng quản lý kho dùng React, ASP.NET Core 10, PostgreSQL và MCP Client/Server C# SDK 2.3.0. MCP Direct Test Mode sử dụng dữ liệu thật và chạy độc lập với API key của LLM.

## Chức năng

- Đăng ký Khách hàng, đăng nhập JWT, refresh token, ba vai trò Admin/Staff/Customer, hồ sơ, đổi mật khẩu và quản lý người dùng.
- Sản phẩm, danh mục, nhà cung cấp, khách hàng; ảnh sản phẩm, CSV và phân trang phía server.
- Nhập/xuất/kiểm kê, nhiều kho, chuyển kho, đơn mua/nhận hàng và đơn bán/giao hàng; transaction, concurrency và chống thực thi trùng.
- Dashboard, báo cáo CSV/Excel/PDF, thông báo, cấu hình, Light/Dark/System và audit metadata.
- MCP Client Lab, Tool Playground, execution history; 25 read tools, 5 write tools, Resources và Prompts. Write tools phải có đề nghị và xác nhận người dùng ở tầng ứng dụng.
- AI Assistant dùng adapter OpenAI-compatible có tool calling qua MCP Client C#. Hội thoại và tool trace được lưu; LLM không tự xác nhận thao tác ghi.

**LLM thật đã được kiểm chứng với GroqCloud, model `openai/gpt-oss-20b`, ngày 2026-10-30.** Trong schema kiểm thử riêng, LLM gọi `GetInventorySummary` qua MCP Client/Server C#, đọc PostgreSQL và trả lời đúng 32 sản phẩm, tổng tồn 0. Đây là dữ liệu mẫu, không phải kết quả đọc kho development. [Kết quả và trace thực tế](../AI-Smart-Inventory-Support/docs/A2-LLM-VERIFICATION.md).

## Tài khoản và phân quyền

Từ `/login`, chọn **Đăng ký ngay** để mở `/register`. Đăng ký thành công tạo tài khoản **Khách hàng (Customer)** rồi chuyển về đăng nhập. Khách hàng chỉ xem/sửa hồ sơ của mình và đổi mật khẩu; không truy cập dữ liệu kho, quản trị, AI hoặc MCP. Admin và Nhân viên đăng nhập vào Dashboard, Khách hàng vào Hồ sơ.

Admin mở **Người dùng**, tìm tài khoản, chọn **Chỉnh sửa → Vai trò → Nhân viên**, lưu và xác nhận để cấp quyền kho. Admin cũng có thể đổi lại Khách hàng, cấp vai trò quản trị hoặc ngừng hoạt động tài khoản. Thay đổi quyền/trạng thái thu hồi access token và refresh session cũ; người dùng đăng nhập lại để nhận quyền mới. Máy chủ bảo vệ quản trị viên đang hoạt động cuối cùng.

Mật khẩu đăng ký mới dài 12–128 ký tự và được lưu bằng `PasswordHasher<User>` với salt riêng. API đăng ký không nhận `role` hay `isActive`, có validation, rate limit, unique username và audit. Vai trò Customer của tài khoản đăng nhập tách biệt với danh bạ đối tác ở module Khách hàng. [Giao diện và kết quả kiểm thử](../AI-Smart-Inventory-Support/docs/UI-ACCOUNTS-VERIFICATION.md).

## Cấu trúc ứng dụng

```text
AI-Smart-Inventory/
├── backend/
│   ├── SmartInventory.Api/             REST, JWT, MCP Client C#, AI orchestration
│   ├── SmartInventory.Application/     Entities, contracts, validation, business policies
│   ├── SmartInventory.Infrastructure/  PostgreSQL, services, migrations, integrations
│   └── SmartInventory.McpServer/       MCP HTTP host, tools, resources, prompts
├── frontend/frontend/                 React + TypeScript + Vite + Material UI
├── scripts/                           EF migrations và backup/restore development
├── .config/                           Phiên bản EF CLI được khóa
├── .github/workflows/ci.yml            Build backend/frontend
├── AI-Smart-Inventory.slnx             Bốn project của ứng dụng
└── global.json                        .NET SDK được khóa
```

Giữ đường dẫn `frontend/frontend`: launcher Vite xử lý workspace có ký tự `#`. `App_Data/uploads/products` là nơi lưu ảnh sản phẩm, API tự tạo khi chạy. Không đưa ảnh runtime hoặc thông tin bí mật vào Git.

## Yêu cầu và cấu hình

.NET SDK 10.0.201, Node 24, PostgreSQL. Python 3.10+ và các công cụ PostgreSQL `psql`, `pg_dump`, `pg_restore` cần cho script kiểm tra/backup/restore.

API và MCP Server dùng chung UserSecretsId trong các file `.csproj` khi chạy Development. Cấu hình riêng bằng .NET User Secrets hoặc biến môi trường, với các khóa:

```text
ConnectionStrings:DefaultConnection = Host=localhost;Port=5432;Database=ai_smart_inventory;Username=<user>;Password=<password>
Jwt:SigningKey = <random-secret-at-least-32-UTF8-bytes>
Bootstrap:Username = <your-admin-name>
Bootstrap:Password = <your-private-12-to-128-character-password>
Bootstrap:Email = <your-email>
```

Ví dụ đặt riêng một giá trị: `dotnet user-secrets set "Jwt:SigningKey" "<private-value>" --project backend/SmartInventory.Api`. Biến môi trường thay dấu `:` bằng `__`, ví dụ `ConnectionStrings__DefaultConnection`. Không lưu secrets trong source hoặc frontend.

Development có cấu hình loopback cho CORS/MCP và JWT issuer/audience. Production cần cấu hình rõ endpoint, hosts, origins và JWT; xem [hướng dẫn triển khai](../AI-Smart-Inventory-Support/docs/DEPLOYMENT.md).

## Build và database

Chạy từ thư mục `AI-Smart-Inventory`. Database development `ai_smart_inventory` phải tồn tại; không reset database đang có dữ liệu.

```sh
dotnet tool restore
dotnet restore AI-Smart-Inventory.slnx --locked-mode
dotnet build AI-Smart-Inventory.slnx --no-restore --disable-build-servers
python3 scripts/dev-db.py --use-user-secrets inspect
python3 scripts/dev-db.py --use-user-secrets backup
ASPNETCORE_ENVIRONMENT=Development bash scripts/ef.sh database update
ASPNETCORE_ENVIRONMENT=Development bash scripts/ef.sh migrations has-pending-model-changes
```

DbContext và migrations nằm trong Infrastructure; API là startup project. `scripts/ef.sh` chỉ định cả hai và `AppDbContext`. Giữ toàn bộ lịch sử migrations. Backup mặc định ở `../backups`; restore chỉ vào database development rỗng được chọn:

```sh
python3 scripts/dev-db.py --use-user-secrets restore ../backups/<archive>.dump --confirm-restore
```

Lần đầu, sau migration và cấu hình Bootstrap riêng:

```sh
dotnet run --project backend/SmartInventory.Api --launch-profile http -- --bootstrap-admin --seed
```

Bootstrap chỉ tạo Admin đầu tiên nếu chưa có Admin hoạt động. Seed bổ sung các SKU mẫu còn thiếu, tồn ban đầu bằng 0; nhập kho phải qua nghiệp vụ chuẩn. Repository không chứa mật khẩu Admin mặc định.

## Chạy ứng dụng

Mở ba Terminal riêng:

```sh
dotnet run --project backend/SmartInventory.McpServer --launch-profile http
```

```sh
dotnet run --project backend/SmartInventory.Api --launch-profile http
```

```sh
cd frontend/frontend
npm ci
npm run dev
```

Frontend: `http://127.0.0.1:5173`; API: `http://localhost:5062`; MCP: `http://localhost:5206/mcp`. Frontend mặc định proxy `/api` và `/uploads` về API 5062; đổi đích bằng `VITE_API_PROXY_TARGET` khi cần. `/health/live` kiểm tra process; API `/health/ready` kiểm tra PostgreSQL và migrations. API không có trang tại `/`.

Build giao diện production:

```sh
npm --prefix frontend/frontend ci
npm --prefix frontend/frontend run build
```

Production cần reverse proxy HTTPS, route `/api` và `/uploads` về API, và SPA fallback về `index.html`. Vite preview không tự proxy API production. Workflow CI trong repository chỉ restore và build bốn project backend cùng frontend.

## AI provider

Cấu hình hiện chọn GroqCloud qua adapter định dạng OpenAI-compatible: `Llm:Provider=OpenAICompatible`, `Llm:BaseUrl=https://api.groq.com/openai/v1`, `Llm:Model=openai/gpt-oss-20b`, `Llm:ReasoningEffort=low` và `Llm:ApiKey`, hoặc các biến `Llm__...`. `OpenAICompatible` là loại adapter; yêu cầu được gửi đến GroqCloud. `ReasoningEffort` là tùy chọn theo model. Khởi động lại API sau khi đổi cấu hình. `ILlmProvider` cho phép đổi adapter hoặc endpoint; kết nối Ollama chưa được xác minh.

Groq hiện dùng `Llm:MaxRateLimitRetries=1`, `Llm:RequestTimeoutSeconds=120`. Chỉ lỗi tốc độ 429 đã phân loại và có `Retry-After` hợp lệ từ 0 đến 60 giây mới được thử lại một lần cho mỗi lượt completion, trong deadline chung. Mặc định retry tắt cho các cấu hình khác; lỗi billing/quota không được thử lại. Việc chờ không thực thi lại tool MCP hoặc tự xác nhận thao tác ghi. Hạn mức nhà cung cấp vẫn có thể làm yêu cầu phải chờ hoặc thất bại; không đổi model tự động.

Trong lúc chat chờ, giao diện đọc tiến độ đã lưu mỗi 2 giây để hiển thị bước thực tế: khám phá tools, gọi LLM, chạy MCP tool hoặc chờ rate limit, kèm thời gian đã chờ và nút Dừng. Rời trang cũng hủy request. Timeout trình duyệt lấy deadline backend cộng 15 giây; cấu hình hiện tại là 135 giây, và khi chưa đọc được deadline thì dùng 120 + 15 giây. Không tự gửi lại toàn bộ chat hoặc thao tác MCP. Backend gửi phần kết quả MCP có cấu trúc một lần cho LLM, giữ nguyên toàn bộ envelope trong lịch sử/audit để tránh dữ liệu lặp mà vẫn tra cứu được đầy đủ. Tốc độ trả lời vẫn phụ thuộc provider và số lượt gọi tool.

Không có key hợp lệ thì AI hiển thị lỗi/trạng thái chưa cấu hình; dữ liệu kho và Direct Mode không phụ thuộc provider. Tool calls có giới hạn thời gian/số vòng, không có câu trả lời giả khi tool lỗi.

## Tài liệu, kiểm thử và dữ liệu đã tách riêng

Các tài liệu và bộ kiểm thử được giữ trong thư mục ngang hàng [AI-Smart-Inventory-Support](../AI-Smart-Inventory-Support/), ngoài source ứng dụng:

- [Tài liệu hệ thống](../AI-Smart-Inventory-Support/docs/): kiến trúc, API, hướng dẫn demo và bằng chứng kiểm thử.
- [Học phần](../AI-Smart-Inventory-Support/academic/): PowerPoint, notebook `.ipynb`, dataset CSV/JSON và câu hỏi thảo luận.
- [Kiểm thử và hướng dẫn chạy](../AI-Smart-Inventory-Support/README.md): C# integration, frontend, browser, Python và các script hỗ trợ.

Chỉ giữ code/cấu hình cần để build, chạy và bảo trì ứng dụng trong thư mục này. Tests và tài liệu được di chuyển để bảo toàn công việc, không được coi là phần đã bỏ.

## Xử lý lỗi thường gặp

- `No DbContext was found`: chạy `dotnet tool restore`, dùng `scripts/ef.sh` từ thư mục ứng dụng.
- PostgreSQL lỗi: kiểm tra service, database, quyền và private config; không in connection string hoặc mật khẩu ra log.
- MCP Disconnected: chạy MCP Server, kiểm tra trusted URL/Host/Origin và cấu hình JWT dùng chung. CRUD vẫn dùng được khi MCP mất kết nối.
- AI unavailable: kiểm tra khóa, model và status provider. Build thành công không chứng minh LLM thật đã hoạt động.
- Phiên đăng nhập: access token nằm trong memory/sessionStorage; refresh token dùng HttpOnly cookie, có rotation và thu hồi phía server. Mật khẩu chỉ được lưu dưới dạng hash ở PostgreSQL.
