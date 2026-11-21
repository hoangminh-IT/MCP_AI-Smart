# Backend AI Smart Inventory

Backend dùng .NET 10, PostgreSQL và MCP C# SDK. Solution gồm bốn project ứng dụng, AppHost điều phối khởi động và một công cụ console phục vụ dữ liệu mẫu.

| Project | Trách nhiệm |
| --- | --- |
| SmartInventory.Application | Entities, contracts, validation và quy tắc nghiệp vụ; không phụ thuộc host API/MCP. |
| SmartInventory.AppHost | Khởi động và dừng API/MCP cùng nhau trong một terminal, gom log và kiểm tra health. |
| SmartInventory.Infrastructure | EF Core, migrations, PostgreSQL, dịch vụ nghiệp vụ và tích hợp provider. |
| SmartInventory.Api | REST, xác thực, MCP Client C#, điều phối AI và composition root. |
| SmartInventory.McpServer | MCP HTTP host, tools, resources và prompts. |
| SmartInventory.DatabaseSeeder | Console C# đăng nhập API và tạo phiếu nhập mẫu qua nghiệp vụ; không phải DB hoặc host chạy thường trực. |

## Build từ thư mục gốc

```sh
dotnet restore AI-Smart-Inventory.slnx --locked-mode
dotnet build AI-Smart-Inventory.slnx --no-restore --disable-build-servers
```

Khởi động cả API và MCP bằng một lệnh:

```sh
dotnet run --project backend/SmartInventory.AppHost
```

AppHost tự build các project phụ thuộc; dùng profile `http` của từng server. MCP được khởi động trước, API chỉ báo sẵn sàng khi PostgreSQL/migrations đạt readiness. `Ctrl+C` dừng cả hai; một server lỗi thì server còn lại được dừng. Không tự tạo dữ liệu hoặc sửa database. Không chạy đồng thời AppHost và các phiên server riêng trên cùng cổng.

Khi cần debug riêng, vẫn có thể chạy từng server ở hai terminal:

```sh
dotnet run --project backend/SmartInventory.Api --launch-profile http
```

```sh
dotnet run --project backend/SmartInventory.McpServer --launch-profile http
```

Thông tin cấu hình riêng và migration nằm trong README gốc. Không commit mật khẩu, JWT hoặc connection string. Không tự chạy seeder mỗi lần khởi động server.

## Đăng ký và khôi phục mật khẩu

Đăng ký công khai bắt buộc họ tên, email, tên đăng nhập, mật khẩu và xác nhận; chỉ tạo vai trò Customer. Quên mật khẩu dùng email SMTP và token một lần có hiệu lực 15 phút, lưu hash trong bảng `password_reset_tokens`. Xem [cấu hình Gmail SMTP](SMTP.md) và tiện ích nhập App Password ẩn `dotnet run --file scripts/configure-gmail.cs`.

## Dữ liệu mẫu

- `--seed` của API tạo danh mục và SKU mẫu còn thiếu, tồn ban đầu bằng 0. Chỉ chạy Development và sau migration.
- `DatabaseSeeder` bổ sung phiếu nhập cho SKU mẫu đang tồn bằng 0 bằng API đã xác thực. Xem [hướng dẫn riêng](SmartInventory.DatabaseSeeder/README.md).
- PostgreSQL lưu dữ liệu thực tế bên ngoài source; notebook trong `experiments/` phục vụ đánh giá, không phải thành phần backend.

## Quy ước bảo trì

- Đặt hợp đồng nghiệp vụ trong Application, triển khai PostgreSQL trong Infrastructure, route trong Api và adapter tool trong McpServer.
- Dùng chung service nghiệp vụ cho API/MCP; không sao chép logic nhập/xuất vào tool hoặc console.
- Giữ đầy đủ migrations và model snapshot. Chúng là lịch sử schema cần thiết, không phải file rác.
- `bin/` và `obj/` là output có thể tái tạo; Git đã bỏ qua chúng. `packages.lock.json` cần giữ để restore nhất quán.
- `Api/App_Data/uploads/` chứa ảnh runtime; không xóa trong quá trình dọn source.
- File `.http` của API cung cấp health probes và được giữ để kiểm tra local.
- Kiểm tra build solution sau khi đổi project reference. Build không thay thế kiểm thử nghiệp vụ hoặc xác minh MCP/PostgreSQL thật.
