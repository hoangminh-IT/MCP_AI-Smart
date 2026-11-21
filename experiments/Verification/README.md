# Kiểm chứng C# / PostgreSQL / MCP và giao diện

Chạy từ thư mục gốc với .NET 10 đã cấu hình User Secrets cho API. PostgreSQL account cần quyền tạo/xóa database kiểm thử. Bộ thử tạo tên DB ngẫu nhiên `test_inventory_<uuid>`, chạy migrations, seed 32 SKU và ba tài khoản có mật khẩu ngẫu nhiên. Nó khởi động API/MCP trên cổng riêng và chỉ xóa đúng database do nó tạo trong finally.

```sh
dotnet build AI-Smart-Inventory.slnx --disable-build-servers
dotnet build experiments/Verification/Verification.csproj --disable-build-servers
dotnet run --project experiments/Verification --no-build
```

Bật QA trình duyệt (Chrome local, hoặc đặt `CHROME_PATH`):

```sh
INVENTORY_BROWSER_QA=1 dotnet run --project experiments/Verification --no-build
```

Bật thực thi notebook (Python environment có nbformat, nbclient, ipykernel, pandas, matplotlib, psycopg):

```sh
INVENTORY_NOTEBOOK_QA=1 dotnet run --project experiments/Verification --no-build
```

Có thể bật cả hai biến cùng lúc. Thông tin DB và tài khoản tạm chỉ truyền trong bộ nhớ; không được ghi vào ô mã notebook. Notebook được lưu output và biểu đồ thật. Notebook hiện có cả phần huấn luyện classifier C# (dataset câu mẫu) và kiểm thử MCP/SQL thật; log Training/Test ở results/training được tách khỏi execution log MCP.

## Bằng chứng

- `../results/verification.json`: bằng chứng lượt nghiệm thu có LLM thật đã được chấp thuận. Các lượt local ghi `local-verification.json`; bộ mật khẩu ghi `password-policy-suite.json`, giữ nguyên log LLM cũ. Chỉ coi suite đạt khi `passed: true`.
- `../results/browser/browser-results.json`: route, viewport, theme và kết quả chuyển hướng theo vai trò.
- `../results/browser/*.png`: ảnh màn hình trên dữ liệu kiểm thử.
- `../outputs/<run-id>/`: snapshot SQL, CSV notebook và execution log; đây là artifact chạy cục bộ được Git bỏ qua.

QA trình duyệt mở 21 màn hình ở desktop sáng/mobile tối và 8 route theo Staff/Customer. Nó kiểm tra render, tràn ngang ở cấp trang và runtime exceptions. Đây không phải chứng nhận mọi tương tác đều không có lỗi. Các cảnh báo API hiển thị trong trang cũng được ghi lại để xem xét.

Process kiểm thử đặt giới hạn request riêng 10000 để mở nhanh nhiều trang; không sửa giới hạn mặc định của ứng dụng. Suite không kiểm chứng tính đúng của rate limiting production hoặc hiệu năng khi nhiều người dùng.

Mặc định LLM key bị xóa khỏi môi trường process kiểm thử. Chế độ `INVENTORY_LIVE_LLM=1` là tùy chọn riêng, gửi output dữ liệu mẫu tới provider đang cấu hình; chỉ chạy khi có chấp thuận cho provider và dữ liệu đó. Không coi các kết quả MCP local là kết quả AI thật.

Nếu process bị buộc dừng trước finally, DB tạm có thể còn lại; kiểm tra chính xác tài nguyên của lần chạy trước khi dọn. Không dọn database theo wildcard.

## Kiểm tra chính sách mật khẩu

```sh
INVENTORY_PASSWORD_QA=1 dotnet run --project experiments/Verification --no-build
```

Chạy riêng chế độ này (không bật cùng browser/notebook/LLM) để giữ giới hạn tần suất xác thực hiện có. Bộ thử kiểm tra đăng ký mật khẩu 1 ký tự, đăng nhập mật khẩu dài hơn 128 ký tự, đổi mật khẩu Unicode có khoảng trắng, tạo/đặt lại mật khẩu bởi Admin, salt riêng, token/refresh session bị thu hồi và từ chối mật khẩu rỗng. Kết quả chỉ ghi tên kiểm tra và trạng thái vào `../results/password-policy-results.json`; không ghi mật khẩu, hash hoặc bearer token.

QA browser còn kiểm tra trường mật khẩu trên Profile/Register không có minLength/maxLength và browser chấp nhận các độ dài 1, 11, 258 trước khi gửi form. Kết quả tại `../results/browser/password-field-results.json`. Điều này kiểm tra HTML validation; hành vi lưu/băm được kiểm tra bằng bộ API/PostgreSQL ở trên.

## Quên mật khẩu qua SMTP và đăng ký đủ thông tin

```sh
INVENTORY_RECOVERY_QA=1 dotnet run --project experiments/Verification --no-build
```

Chạy riêng chế độ này. SMTP local chỉ nhận thư trong bộ nhớ, không chuyển tiếp ra Internet; process test xóa SMTP username/password cấu hình thật khỏi môi trường của nó. API và PostgreSQL dùng DB tạm.

Kiểm tra email có link, DB chỉ lưu hash, response không trả token, thông báo chung khi tài khoản không tồn tại, đặt lại mật khẩu, thu hồi phiên, chặn replay/hết hạn, thay thế link cũ và giới hạn request. Các ca đăng ký lần lượt bỏ từng trường bắt buộc và thử chèn role Admin phải trả HTTP 400 mà không tạo tài khoản.

Kết quả: `../results/password-recovery-results.json` và `../results/password-recovery-suite.json`. QA browser bổ sung bốn kiểm tra public auth tại `../results/browser/public-auth-results.json`: đủ 5 lỗi trường đăng ký, link Quên mật khẩu, hai trường thông tin khôi phục bắt buộc và reset từ chối link thiếu token. Đây là SMTP local; việc gửi Gmail thật vẫn cần App Password hợp lệ.

Sau khi di chuyển thư mục, kiểm tra đường dẫn bằng `dotnet run --project experiments/Verification --no-build -- --check-paths`. Lệnh này không tạo database hoặc gọi provider.
