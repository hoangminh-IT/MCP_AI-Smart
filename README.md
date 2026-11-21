# AI Smart Inventory

**MCP.2505 — Model Context Protocol, C# SDK**

Ứng dụng quản lý kho với React, ASP.NET Core, PostgreSQL và MCP Client/Server C#. Người dùng thao tác qua giao diện hoặc AI Assistant; API và MCP dùng chung các service nghiệp vụ. Repo kèm notebook, data, log Training/Test, báo cáo và câu hỏi trắc nghiệm phục vụ học phần.

## Mục lục

- [Chức năng](#chức-năng)
- [Cấu trúc project](#cấu-trúc-project)
- [Yêu cầu môi trường](#yêu-cầu-môi-trường)
- [Cài đặt lần đầu](#cài-đặt-lần-đầu)
- [Chạy ứng dụng](#chạy-ứng-dụng)
- [Tài khoản và phân quyền](#tài-khoản-và-phân-quyền)
- [Cấu hình AI và MCP](#cấu-hình-ai-và-mcp)
- [Notebook, data và Training/Test](#notebook-data-và-trainingtest)
- [Chạy kiểm chứng](#chạy-kiểm-chứng)
- [Bảo trì và đưa lên GitHub](#bảo-trì-và-đưa-lên-github)
- [Xử lý lỗi](#xử-lý-lỗi)
- [Tài liệu chi tiết](#tài-liệu-chi-tiết)

## Chức năng

| Nhóm | Nội dung |
| --- | --- |
| Tài khoản | Đăng ký, đăng nhập, JWT/refresh token, hồ sơ, đổi mật khẩu, quên mật khẩu qua SMTP; Admin quản lý tài khoản và vai trò. |
| Danh mục và đối tác | Sản phẩm, danh mục, nhà cung cấp, khách hàng, ảnh sản phẩm, CSV và phân trang phía server. |
| Kho hàng | Nhập, xuất, điều chỉnh tồn, nhiều kho, chuyển kho, lịch sử biến động và chống thực thi trùng. |
| Mua/bán | Đơn mua, duyệt đơn và nhận hàng; đơn bán và giao hàng. |
| Theo dõi | Dashboard, biểu đồ, báo cáo CSV/Excel/PDF, thông báo và nhật ký kiểm toán. |
| MCP | Client Lab, Tool Playground, lịch sử thực thi; 25 read tools, 5 write tools, 2 Resources và 2 Prompts. |
| AI Assistant | Hội thoại, tool calling qua MCP Client C#, lưu tiến độ và trace; thao tác ghi chờ xác nhận người dùng. |
| Giao diện | React/Material UI, sidebar responsive, bảng và biểu mẫu dùng chung, Light/Dark/System. |

Luồng dữ liệu chính: **Giao diện → API → MCP Client C# → MCP Server C# → service nghiệp vụ → PostgreSQL**. Các màn hình CRUD gọi REST API; AI Assistant dùng LLM để đề xuất tool call trong luồng MCP.

## Cấu trúc project

```text
AI-Smart-Inventory/
├── backend/
│   ├── SmartInventory.AppHost/         Launcher chạy API và MCP cùng nhau
│   ├── SmartInventory.Api/             REST, xác thực, MCP Client và AI orchestration
│   ├── SmartInventory.Application/     Entities, contracts và quy tắc nghiệp vụ
│   ├── SmartInventory.Infrastructure/  EF Core, PostgreSQL, migrations và integrations
│   ├── SmartInventory.McpServer/       MCP HTTP host, Tools, Resources và Prompts
│   └── SmartInventory.DatabaseSeeder/  Console C# tạo phiếu nhập mẫu qua API
├── frontend/                          React + TypeScript + Vite + Material UI
├── experiments/
│   ├── MCP_CSharp_Inventory_Experiments.ipynb
│   ├── data/                          Dataset và các bộ cấu hình
│   ├── Training/                      Bộ phân loại chọn read tool bằng C#
│   ├── Verification/                  Kiểm chứng API, MCP, PostgreSQL và giao diện
│   ├── results/                       Log, model, CSV, biểu đồ và ảnh kiểm thử
│   ├── reports/                       Báo cáo thực nghiệm và slide HTML
│   ├── quiz/                          20 câu trắc nghiệm và đáp án TXT
│   └── requirements.txt               Thư viện Python cho notebook
├── docs/                              Kế hoạch hoàn thiện và phạm vi nghiệm thu
├── scripts/                           EF CLI, backup/restore và cấu hình SMTP
├── deployment/                        Mẫu biến môi trường triển khai
├── .config/dotnet-tools.json           Phiên bản EF CLI được khóa
├── AI-Smart-Inventory.slnx             6 project backend + Training + Verification
├── Directory.Build.props              Bật lưu NuGet lock file
└── global.json                        .NET SDK 10.0.201
```

Notebook, data và log được giữ một bản trong `experiments/`. Báo cáo ở `experiments/reports/`, trắc nghiệm ở `experiments/quiz/`; kế hoạch ở `docs/`.

## Yêu cầu môi trường

| Thành phần | Phiên bản/vai trò |
| --- | --- |
| .NET SDK | **10.0.201**, khóa bằng [global.json](global.json); `rollForward` tắt. |
| EF CLI | **10.0.13**, cài qua `dotnet tool restore`. |
| MCP C# SDK | **2.3.0**, được khóa trong các project và NuGet lock file. |
| Node.js | **24**, dùng npm và `frontend/package-lock.json`. |
| PostgreSQL | Máy chủ chạy được và tài khoản có quyền trên database ứng dụng. |
| Python | Dùng cho notebook và script bảo trì; package notebook ở [requirements.txt](experiments/requirements.txt). Phiên bản đã kiểm tra nằm trong [environment.json](experiments/results/environment.json). |
| PostgreSQL CLI | `psql`, `pg_dump`, `pg_restore` khi dùng công cụ kiểm tra/backup/restore. |
| Chrome | Cần khi bật QA giao diện; có thể chỉ định bằng `CHROME_PATH`. |

API/MCP và công cụ nhập dữ liệu kho viết bằng C#. Notebook dùng Python để điều phối Training C#, đọc SQL, kiểm tra kết quả và vẽ biểu đồ. Chạy ứng dụng thông thường không cần mở notebook.

## Cài đặt lần đầu

Các lệnh dưới đây chạy từ thư mục gốc `AI-Smart-Inventory`, trừ khi có chỉ dẫn khác. Các ví dụ gán biến môi trường dùng cú pháp macOS/Linux.

### 1. Khôi phục công cụ và dependencies

```sh
dotnet tool restore
dotnet restore AI-Smart-Inventory.slnx --locked-mode
dotnet build AI-Smart-Inventory.slnx --no-restore --disable-build-servers
npm --prefix frontend ci
```

### 2. Tạo database và cấu hình riêng

Tạo database rỗng `ai_smart_inventory` bằng pgAdmin hoặc công cụ PostgreSQL. Đặt thông tin kết nối, JWT và tài khoản Admin đầu tiên trong .NET User Secrets; thay các giá trị `<...>` bằng cấu hình của máy:

```sh
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=ai_smart_inventory;Username=<postgres-user>;Password=<postgres-password>" --project backend/SmartInventory.Api
dotnet user-secrets set "Jwt:SigningKey" "<chuoi-ngau-nhien-it-nhat-32-byte-UTF8>" --project backend/SmartInventory.Api
dotnet user-secrets set "Bootstrap:Username" "<admin-username>" --project backend/SmartInventory.Api
dotnet user-secrets set "Bootstrap:Password" "<admin-password>" --project backend/SmartInventory.Api
dotnet user-secrets set "Bootstrap:Email" "<admin-email>" --project backend/SmartInventory.Api
```

API và MCP Server dùng chung `UserSecretsId` khi chạy Development. Có thể thay bằng biến môi trường với dấu `__`, ví dụ `ConnectionStrings__DefaultConnection` và `Jwt__SigningKey`. Repo không chứa tài khoản/mật khẩu Admin mặc định.

### 3. Áp dụng migrations

```sh
ASPNETCORE_ENVIRONMENT=Development bash scripts/ef.sh database update
ASPNETCORE_ENVIRONMENT=Development bash scripts/ef.sh migrations has-pending-model-changes
```

`AppDbContext` và migrations nằm trong Infrastructure; API là startup project. [scripts/ef.sh](scripts/ef.sh) chỉ định đúng các project và context. Với DB đang có dữ liệu, tạo backup trước khi cập nhật schema; xem phần [bảo trì](#bảo-trì-và-đưa-lên-github).

### 4. Tạo Admin đầu tiên và danh mục mẫu

```sh
dotnet run --project backend/SmartInventory.Api --launch-profile http -- --bootstrap-admin --seed
```

Lệnh này chỉ chạy trong Development, yêu cầu đã migrate và **kết thúc sau khi hoàn tất**. Bootstrap chỉ tạo Admin đầu tiên khi chưa có Admin hoạt động; seed bổ sung danh mục/SKU mẫu còn thiếu với tồn ban đầu bằng 0.

Để tạo phiếu nhập mẫu sau khi đã chạy backend:

```sh
dotnet run --project backend/SmartInventory.DatabaseSeeder
```

Console yêu cầu tài khoản Admin/Staff hiện tại và mật khẩu nhập ẩn, sau đó gọi API để ghi PostgreSQL qua nghiệp vụ kho. Nó không tự tạo database hoặc migrate. Xem [DatabaseSeeder README](backend/SmartInventory.DatabaseSeeder/README.md) để biết SKU, số lượng và điều kiện nhập.

## Chạy ứng dụng

### Backend: một terminal cho API và MCP

```sh
dotnet run --project backend/SmartInventory.AppHost
```

AppHost là launcher C#: build các project phụ thuộc, khởi động MCP trước rồi API, gom log `[MCP]`/`[API]` và kiểm tra health. `Ctrl+C` dừng cả hai; nếu một server dừng, launcher dừng server còn lại. AppHost dùng profile `http`, không tự migrate hoặc seed. Dừng các phiên API/MCP cũ trước khi chạy để tránh trùng cổng.

### Frontend: terminal thứ hai

```sh
npm --prefix frontend run dev
```

| Dịch vụ | Địa chỉ local |
| --- | --- |
| Giao diện | [http://127.0.0.1:5173](http://127.0.0.1:5173) |
| REST API | `http://localhost:5062/api` |
| MCP HTTP | `http://localhost:5206/mcp` |
| API liveness | `http://localhost:5062/health/live` |
| API readiness | `http://localhost:5062/health/ready` |

Readiness của API kiểm tra PostgreSQL và migrations. API không cung cấp trang web tại `/`; mở giao diện ở cổng 5173. Vite proxy `/api` và `/uploads` về API 5062; thay bằng `VITE_API_PROXY_TARGET` khi cần. Launcher Vite xử lý đường dẫn workspace có ký tự `#`.

Khi debug riêng, chạy từng server theo [backend README](backend/README.md). Build frontend bằng `npm --prefix frontend run build`; triển khai cần reverse proxy HTTPS, route `/api` và `/uploads` về API, cùng SPA fallback về `index.html`. Vite preview không tự cung cấp proxy API production. Mẫu cấu hình ở [deployment/](deployment/); .NET không tự nạp các file `.env.example` này.

## Tài khoản và phân quyền

### Đăng ký và quyền truy cập

Đăng ký yêu cầu đủ **họ tên, email, tên đăng nhập, mật khẩu, xác nhận mật khẩu**. Tài khoản công khai luôn có vai trò **Customer**, không được tự chọn Admin/Staff. Đăng ký xong chuyển về đăng nhập; Customer vào Hồ sơ, Admin/Staff vào Dashboard.

| Chức năng | Admin | Staff | Customer |
| --- | --- | --- | --- |
| Xem/sửa hồ sơ và đổi mật khẩu của mình | Có | Có | Có |
| Đọc kho, sản phẩm, đối tác, báo cáo | Có | Có | Không |
| Nhập/xuất/chuyển kho, xử lý đơn hàng | Có | Theo quy tắc nghiệp vụ | Không |
| Tạo/sửa danh mục, sản phẩm, đối tác, kho | Có | Không | Không |
| Điều chỉnh tồn và duyệt đơn mua | Có | Không | Không |
| Quản lý người dùng, audit và sửa cấu hình hệ thống | Có | Không | Không |
| AI Assistant và MCP | Theo quyền tool | Theo quyền tool | Không |

Admin mở **Người dùng → Chỉnh sửa → Vai trò** để cấp quyền Nhân viên hoặc Quản trị viên. Thay đổi quyền/trạng thái thu hồi phiên cũ; tài khoản đăng nhập lại để nhận quyền mới. Hệ thống bảo vệ Admin đang hoạt động cuối cùng. Backend kiểm tra quyền độc lập với menu và route guard của giao diện.

### Mật khẩu và khôi phục qua email

Mật khẩu không được rỗng hoặc chỉ chứa khoảng trắng; ứng dụng không áp đặt giới hạn độ dài riêng ở form/contract hiện tại. Khoảng trắng trong mật khẩu hợp lệ được giữ nguyên. Mật khẩu được **băm bằng `PasswordHasher<User>` với salt riêng**, không lưu dạng đọc được. Access token nằm trong memory/sessionStorage; refresh token dùng HttpOnly cookie, có rotation và thu hồi.

Trang đăng nhập có **Quên mật khẩu?**. Nhập tên đăng nhập và email đã lưu để nhận link một lần, hiệu lực 15 phút; đặt lại thành công thu hồi phiên cũ. Cấu hình Gmail bằng tiện ích nhập App Password ẩn:

```sh
dotnet run --file scripts/configure-gmail.cs
```

Khởi động lại AppHost sau khi lưu cấu hình. SMTP thật cần thông tin đăng nhập hợp lệ; bộ kiểm thử SMTP local không chứng nhận việc gửi Gmail. Email hồ sơ phải là địa chỉ nhận được thư. Xem [hướng dẫn SMTP](backend/SMTP.md).

## Cấu hình AI và MCP

MCP Direct Mode khám phá schema và thực thi tool qua C# mà không cần LLM key. Read tools chạy trực tiếp. Write tools tạo đề nghị ở tầng ứng dụng, chờ người dùng xác nhận rồi mới ghi; service dùng transaction và request ID để chống ghi trùng. Các write tools gồm `CreateStockReceipt`, `CreateStockIssue`, `AdjustInventory`, `CreatePurchaseOrder` và `UpdatePurchaseOrderStatus`.

AI Assistant dùng adapter OpenAI-compatible. Cấu hình GroqCloud đã dùng trong thực nghiệm:

```sh
dotnet user-secrets set "Llm:Provider" "OpenAICompatible" --project backend/SmartInventory.Api
dotnet user-secrets set "Llm:BaseUrl" "https://api.groq.com/openai/v1" --project backend/SmartInventory.Api
dotnet user-secrets set "Llm:Model" "openai/gpt-oss-20b" --project backend/SmartInventory.Api
dotnet user-secrets set "Llm:ReasoningEffort" "low" --project backend/SmartInventory.Api
dotnet user-secrets set "Llm:ApiKey" "<provider-api-key>" --project backend/SmartInventory.Api
```

`OpenAICompatible` là loại adapter; endpoint quyết định nhà cung cấp. Khởi động lại API sau khi đổi cấu hình. Không có key hợp lệ thì phần AI báo chưa cấu hình/lỗi, còn CRUD và MCP Direct Mode vẫn hoạt động.

Mẫu triển khai dùng timeout 120 giây và tối đa một lần retry cho lỗi tốc độ 429 đã phân loại, có `Retry-After` hợp lệ. Backend không thử lại lỗi billing/quota hoặc tự thực thi lại write tool. Giao diện hiển thị tiến độ thực tế và cho phép hủy; thời gian trả lời phụ thuộc provider và số lượt tool call.

[Báo cáo MCP/LLM](experiments/reports/BAO-CAO-THUC-NGHIEM.md) lưu bằng chứng một câu hỏi tổng hợp đã gọi `GetInventorySummary` qua provider thật trên DB mẫu. Đây là kết quả của lượt đã ghi nhận, chưa phải benchmark độ chính xác AI trên mọi câu hỏi.

## Notebook, data và Training/Test

Mở [MCP_CSharp_Inventory_Experiments.ipynb](experiments/MCP_CSharp_Inventory_Experiments.ipynb). Notebook gồm hai phần: **A — huấn luyện bộ chọn tool bằng C#** và **B — thực thi MCP/SQL trên PostgreSQL kiểm thử**.

### Bộ file theo yêu cầu học phần

| Yêu cầu | File/bằng chứng |
| --- | --- |
| Notebook và data | [Notebook](experiments/MCP_CSharp_Inventory_Experiments.ipynb), [data README](experiments/data/README.md), [160 câu mẫu](experiments/data/mcp_tool_intents.csv), [snapshot 32 sản phẩm](experiments/data/inventory_postgresql_snapshot.csv). |
| Tình huống chạy thử | [Báo cáo MCP](experiments/reports/BAO-CAO-THUC-NGHIEM.md): phân trang, ngưỡng tồn, ID/SKU, quyền, xác nhận ghi, replay, rollback và LLM. |
| Các bộ tham số | [10 cấu hình Training](experiments/data/training_configurations.json) và [10 bộ arguments MCP](experiments/data/mcp_read_configurations.json). |
| Kết quả và so sánh | [Báo cáo Training/Test](experiments/reports/TRAINING-TEST-REPORT.md), [CSV từng cấu hình](experiments/results/training/20261121T1206152520820Z/experiment_results.csv), [biểu đồ](experiments/results/training/20261121T1206152520820Z/training_comparison.png). |
| Log Training | [training_log.csv](experiments/results/training/20261121T1206152520820Z/training_log.csv) và [training_log.jsonl](experiments/results/training/20261121T1206152520820Z/training_log.jsonl): 591 bản ghi, gồm epoch 0 của từng cấu hình. |
| Log Test | [test_log.jsonl](experiments/results/training/20261121T1206152520820Z/test_log.jsonl): 10 sự kiện; [test_predictions.csv](experiments/results/training/20261121T1206152520820Z/test_predictions.csv): 320 dự đoán. |
| Nhận xét và cải tiến | Hai báo cáo trình bày kết quả, lỗi phân loại, giới hạn dữ liệu và hướng cải tiến. |
| TXT 10–20 câu trắc nghiệm | [20 câu A/B/C/D](experiments/quiz/20_CAU_HOI_TRAC_NGHIEM.txt), [đáp án và giải thích riêng](experiments/quiz/DAP_AN_TRAC_NGHIEM.txt). |
| Thuyết trình | [PRESENTATION.html](experiments/reports/PRESENTATION.html): 11 slide offline, chuyển bằng phím trái/phải hoặc nút. |

### Chạy riêng Training

```sh
dotnet run --project experiments/Training
dotnet run --project experiments/Training -- --predict "Sản phẩm nào sắp hết hàng"
```

Mô hình TF-IDF + softmax bằng C# chọn một trong 8 read tool. Dataset gồm **96 Train / 32 Validation / 32 Test**; từ vựng/IDF chỉ fit trên Train. Cấu hình được chọn bằng Validation trước khi xem Test. Chạy Training không cần DB, SMTP hoặc LLM key; log/model mới lưu trong `experiments/results/training/<run-id>/`, `latest.json` trỏ tới lượt mới nhất.

### Chạy notebook

Cài package trong môi trường Python riêng, rồi chọn kernel tương ứng trong VS Code/Jupyter:

```sh
python -m pip install -r experiments/requirements.txt
```

Phần A chạy độc lập. Để chạy cả Phần B bằng runner C#:

```sh
dotnet build AI-Smart-Inventory.slnx --disable-build-servers
INVENTORY_NOTEBOOK_QA=1 dotnet run --project experiments/Verification --no-build
```

Runner tạo DB test riêng, migrate/seed, khởi động API/MCP trên cổng riêng, truyền đầu vào trong bộ nhớ, lưu output notebook rồi dọn đúng DB đã tạo. Tài khoản PostgreSQL cần quyền tạo/xóa DB kiểm thử. Chế độ này tắt LLM mặc định. Chạy thủ công Phần B cần API/MCP và SQL dùng cùng database/schema; tên DB tạm trong output cũ đã được dọn, không dùng lại để kết nối.

### Kết quả đã lưu và giới hạn

| Nội dung | Kết quả được ghi nhận |
| --- | --- |
| Training/Test | T10 chọn bằng Validation; Test accuracy **93,75%**, macro-F1 **0,9365**; baseline **12,5%**. |
| Notebook | **11/11 ô mã** đã chạy, **0 error output**, **2 biểu đồ PNG nhúng**. |
| Tích hợp model → MCP | **8/8** câu cố định chọn đúng tool và thực thi thành công; đây là tập con kiểm tra kết nối. |
| Nghiệm thu MCP/ứng dụng gốc | **43** lời gọi HTTP được kiểm tra; QA giao diện **50** lượt route/viewport/quyền. Chi tiết ở báo cáo và log của lượt gốc. |

Dataset câu hỏi là dữ liệu tổng hợp cho bài, chưa phải hội thoại người dùng thu thập độc lập. Snapshot kho là dữ liệu mẫu đọc từ PostgreSQL thật, chưa phải lịch sử kinh doanh. Training huấn luyện **mô hình phụ chọn tool**, chưa fine-tune LLM hoặc tích hợp vào AI Assistant production. Các cấu hình có thể cùng accuracy nhưng khác loss/F1; giữ nguyên kết quả quan sát.

Xem [results README](experiments/results/README.md) để phân biệt các run ID. Log Training, log Test và execution log MCP là các bằng chứng riêng. Output notebook có thể giữ đường dẫn tại thời điểm chạy cũ; dùng hướng dẫn hiện tại để tái hiện.

## Chạy kiểm chứng

Build solution trước, rồi chạy:

```sh
dotnet run --project experiments/Verification --no-build -- --check-paths
dotnet run --project experiments/Verification --no-build
```

Lệnh đầu chỉ kiểm tra đường dẫn, không dùng DB/provider. Lệnh thứ hai thực thi suite API/MCP/PostgreSQL trên DB test riêng, mặc định tắt LLM.

| Chế độ bổ sung | Lệnh từ thư mục gốc |
| --- | --- |
| Notebook | `INVENTORY_NOTEBOOK_QA=1 dotnet run --project experiments/Verification --no-build` |
| Giao diện Chrome | `INVENTORY_BROWSER_QA=1 dotnet run --project experiments/Verification --no-build` |
| Chính sách mật khẩu | `INVENTORY_PASSWORD_QA=1 dotnet run --project experiments/Verification --no-build` |
| Khôi phục qua SMTP local | `INVENTORY_RECOVERY_QA=1 dotnet run --project experiments/Verification --no-build` |

Có thể kết hợp browser/notebook; chế độ password và recovery phải chạy riêng để giữ giới hạn xác thực. LLM thật là tùy chọn `INVENTORY_LIVE_LLM=1`, chỉ bật khi có chấp thuận cho dữ liệu mẫu và provider được cấu hình. SMTP local không gửi thư ra Internet. Chi tiết điều kiện và nơi lưu log ở [Verification README](experiments/Verification/README.md).

## Bảo trì và đưa lên GitHub

Kiểm tra và backup database bằng script bảo trì:

```sh
python3 scripts/dev-db.py --use-user-secrets inspect
python3 scripts/dev-db.py --use-user-secrets backup
```

Backup mặc định ở `../backups`; restore chỉ vào database development rỗng đã chọn:

```sh
python3 scripts/dev-db.py --use-user-secrets restore "../backups/<archive>.dump" --confirm-restore
```

Giữ source, toàn bộ migrations/model snapshot, `packages.lock.json`, `frontend/package-lock.json`, các file SDK/tool và bộ thực nghiệm trong Git. `.gitignore` bỏ qua secrets, môi trường cục bộ, DB dump, `bin/`, `obj/`, `node_modules/`, `dist/`, output notebook phát sinh và checkpoint.

Ảnh runtime nằm trong `backend/SmartInventory.Api/App_Data/uploads/products/`, được API tạo khi cần. Database PostgreSQL nằm ngoài source. Kết quả mẫu có trong `experiments/results/`; repo không kèm DB dump hoặc thông tin đăng nhập riêng. Mẫu production ở [deployment/](deployment/) cần được cấp biến môi trường/secret store và cấu hình host, origin, JWT, HTTPS phù hợp.

## Xử lý lỗi

| Hiện tượng | Cách kiểm tra |
| --- | --- |
| NuGet restore hoặc `.slnx` báo thiếu project | Kiểm tra SDK bằng `dotnet --info`, cài đúng phiên bản trong `global.json`, rồi chạy `dotnet restore AI-Smart-Inventory.slnx --locked-mode`. Các project thực nghiệm hiện ở `experiments/`. |
| `No DbContext was found` | Chạy `dotnet tool restore` và dùng `bash scripts/ef.sh ...` từ gốc project. |
| AppHost không báo READY | Xem log `[API]`/`[MCP]`, kiểm tra PostgreSQL, migrations và cổng 5062/5206 có bị chiếm không. |
| API `/` trả 404 | Mở giao diện cổng 5173; kiểm tra API bằng `/health/live` hoặc `/health/ready`. |
| MCP Disconnected | Kiểm tra server, URL `/mcp`, allowed hosts/origins và JWT/DB dùng chung. |
| AI chưa cấu hình, lỗi hoặc chờ lâu | Kiểm tra provider/key/model, timeout, trạng thái và hạn mức của provider; không tự gửi lại thao tác ghi. |
| Không đổi/khôi phục được mật khẩu | Đối chiếu lỗi trường nhập, mật khẩu hiện tại, xác nhận, email hồ sơ, SMTP và hiệu lực link reset. |
| Notebook thiếu module hoặc chưa có DB | Chọn đúng kernel, cài `experiments/requirements.txt`; chạy runner cho Phần B hoặc chuẩn bị DB test đã migrate/seed. |
| Đã đổi quyền nhưng chưa vào được trang | Đăng nhập lại để nhận phiên theo quyền mới. |

## Tài liệu chi tiết

| Tài liệu | Nội dung |
| --- | --- |
| [Backend](backend/README.md) | Trách nhiệm project, AppHost, dữ liệu và bảo trì. |
| [Frontend](frontend/README.md) | Giao diện, Vite, route và phiên đăng nhập. |
| [SMTP](backend/SMTP.md) | Gmail, các khóa cấu hình và luồng khôi phục mật khẩu. |
| [Dữ liệu mẫu C#](backend/SmartInventory.DatabaseSeeder/README.md) | Tạo phiếu nhập qua API nghiệp vụ. |
| [Thực nghiệm](#notebook-data-và-trainingtest) | Notebook, data, Training, log, báo cáo và trắc nghiệm. |
| [Bộ kiểm chứng](experiments/Verification/README.md) | API/MCP/PostgreSQL, browser, mật khẩu và SMTP local. |
| [Kế hoạch hoàn thiện](docs/KE-HOACH-HOAN-THIEN-THUC-NGHIEM.md) | Phạm vi, tình huống, giao diện, quyền và tiêu chí nghiệm thu. |
