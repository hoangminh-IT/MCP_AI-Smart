# Gmail SMTP và Quên mật khẩu

Ứng dụng gửi từ địa chỉ Gmail đã cấu hình với tên hiển thị **Smart Inventory**. Tài khoản ứng dụng nhận liên kết tại email trong hồ sơ; địa chỉ nhận có thể khác địa chỉ gửi.

## Hoàn tất cấu hình riêng

1. Đăng nhập tài khoản Gmail gửi thư, bật xác minh hai bước nếu chưa bật.
2. Tạo App Password cho Smart Inventory tại [Google App Passwords](https://myaccount.google.com/apppasswords). Không dùng mật khẩu đăng nhập Gmail và không gửi App Password trong chat hoặc đưa vào Git.
3. Từ thư mục gốc dự án, chạy:

```sh
dotnet run --file scripts/configure-gmail.cs
```

Nhập Gmail gửi thư, App Password vào ô ẩn, rồi xác nhận URL frontend. Tiện ích lưu vào User Secrets của API qua stdin, không để mật khẩu trong command line hoặc file source. Nó không gửi thư thử. Khởi động lại AppHost sau khi lưu cấu hình.

Gmail dùng `smtp.gmail.com:587` với STARTTLS. [Tài liệu SMTP chính thức](https://developers.google.com/workspace/gmail/imap/imap-smtp) và [điều kiện dùng App Password](https://support.google.com/accounts/answer/185833) giải thích cấu hình và các trường hợp tài khoản không có tùy chọn này.

## Các khóa cấu hình

| Khóa trong PasswordRecovery | Ý nghĩa |
| --- | --- |
| Host / Port | SMTP host và cổng STARTTLS; Gmail: smtp.gmail.com / 587. |
| EnableSsl | true cho Gmail; không tắt TLS với máy chủ bên ngoài. |
| Username / Password | Gmail gửi thư và App Password, lưu riêng. |
| FromEmail / FromName | Địa chỉ gửi và tên hiển thị Smart Inventory. |
| PublicBaseUrl | URL frontend dùng trong liên kết, local mặc định http://127.0.0.1:5173; production dùng HTTPS. |

Production dùng biến môi trường `PasswordRecovery__...` hoặc secret store của môi trường triển khai. Không lấy URL từ Host header của request. HTTP và SMTP không TLS chỉ được chấp nhận cho loopback trong Development/Testing để kiểm thử.

## Luồng sử dụng

1. Trang đăng nhập → **Quên mật khẩu?**.
2. Nhập tên đăng nhập và đúng email đã lưu trong tài khoản.
3. Nhận email, mở liên kết có hiệu lực 15 phút.
4. Nhập và xác nhận mật khẩu mới. Token chỉ dùng được một lần; phiên đăng nhập cũ bị thu hồi.

Nếu Gmail chưa có App Password, API trả trạng thái chưa sẵn sàng cho mọi tài khoản, không báo đã gửi email. Khi SMTP đã cấu hình, kết quả yêu cầu là thông báo chung để tránh tiết lộ tài khoản tồn tại. Lỗi giao nhận được ghi ở server bằng loại lỗi, không ghi email, mật khẩu hoặc link/token.

Email trong tài khoản phải là địa chỉ bạn nhận được thư. Ví dụ địa chỉ `@inventory.local` trong tài khoản mẫu không thể nhận thư Gmail; hãy cập nhật email trong Hồ sơ trước khi cần khôi phục. Tiện ích cấu hình SMTP không tự đổi email hồ sơ hoặc mật khẩu người dùng.

## Lưu trữ và kiểm thử

- Bảng `password_reset_tokens` chỉ lưu SHA-256 của token ngẫu nhiên, thời gian hết hạn và trạng thái đã dùng.
- Link chứa token trong fragment `#token=...`; phần này không gửi trong HTTP URL/referrer. Trang reset xóa fragment khỏi URL sau khi đọc.
- Yêu cầu link mới thay thế link trước; đổi/đặt lại mật khẩu hoặc thay token version làm link cũ mất hiệu lực.
- Giới hạn 5 request khôi phục mỗi phút/IP áp dụng cho cả yêu cầu và reset.
- Đăng ký bắt buộc họ tên, email, username, mật khẩu, xác nhận; backend không nhận role/isActive từ đăng ký công khai.
- Bộ thử `INVENTORY_RECOVERY_QA=1` chạy qua SMTP local cô lập và PostgreSQL thật; không gửi email bên ngoài. Điều này không chứng nhận đăng nhập Gmail thành công nếu chưa có App Password.
