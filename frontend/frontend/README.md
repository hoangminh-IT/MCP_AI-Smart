# Smart Inventory frontend

React 19, TypeScript, Vite 7, Material UI, React Router, Axios, TanStack Query và Recharts. Dùng Node 24; phiên bản dependencies được khóa trong `package.json` và `package-lock.json`.

```sh
npm ci
npm run dev
```

Frontend chạy tại `http://127.0.0.1:5173`. Khởi động API riêng tại `http://localhost:5062`; Vite proxy `/api` và `/uploads` tới API. Đổi địa chỉ development khi cần:

```sh
VITE_API_PROXY_TARGET=http://127.0.0.1:5062 npm run dev
```

`VITE_API_PROXY_TARGET` chỉ là địa chỉ public của API development, không chứa token hoặc API key. Backend quản lý toàn bộ secrets và kiểm tra quyền.

`scripts/vite.mjs` xử lý ký tự `#` trong đường dẫn workspace bằng symlink tạm, dọn khi process kết thúc. Mỗi process có cache tối ưu dependencies riêng, tránh các Vite development/test server chạy song song ghi đè dependency URLs của nhau. Dùng npm scripts để chạy Vite; script dev nhận `--host`, `--port` và `--strictPort`.

Giao diện gồm đăng nhập/đăng ký/hồ sơ, dashboard, sản phẩm/danh mục, suppliers/customers, tồn nhiều kho/chuyển kho, movements, purchase/sales orders, reports, notifications, users, settings và audit. Theme navy/blue/teal, sidebar responsive, bảng và biểu mẫu đồng bộ, hỗ trợ Light/Dark/System. Các trang dùng API thật với loading/error/empty states; thao tác và menu theo quyền Admin/Staff/Customer được backend bảo vệ độc lập.

Giao diện nghiệp vụ được đồng bộ ngày 2026-10-31: sidebar và thanh trên gọn hơn, tiêu đề không lặp nhóm menu, bộ lọc dùng chung `FilterBar`, bảng có mật độ đọc thống nhất. Dashboard ưu tiên bốn chỉ số chính, dải số liệu phụ, biểu đồ và công việc cần xử lý; xuất báo cáo qua menu CSV/Excel/PDF. Reports, Settings và Profile được chia lại theo nhóm nội dung; nhật ký kiểm toán có bộ lọc chi tiết mở rộng.

MCP Client Lab có tên chức năng và mô tả tiếng Việt, lọc công cụ tra cứu/thay đổi dữ liệu; tên SDK và schema gốc vẫn được giữ. Playground tách vùng nhập/kết quả, có tab biểu mẫu/JSON; tùy chọn bỏ tham số, gửi null hoặc về mặc định nằm trong menu từng trường. Mô tả kỹ thuật, Resources/Prompts và ví dụ C# mở khi cần. Xem [báo cáo kiểm tra giao diện](../../../AI-Smart-Inventory-Support/docs/UI-REDESIGN-VERIFICATION.md).

`/register` tạo tài khoản Customer rồi chuyển về `/login`; không tự cấp phiên hoặc cho chọn vai trò. Customer đăng nhập vào `/profile`, chỉ cập nhật hồ sơ/đổi mật khẩu và không khởi chạy truy vấn kho hoặc thông báo. Admin/Staff vào `/dashboard`. Admin phân quyền trong trang Người dùng với bộ lọc ba vai trò và bước xác nhận thay đổi quyền; phiên cũ bị thu hồi phía server. Route guard chuyển Customer về hồ sơ khi truy cập trang nghiệp vụ. Mật khẩu mới 12–128 ký tự; form có xác nhận và nút hiện/ẩn mật khẩu.

Trang đăng nhập/đăng ký dùng ảnh chụp thật phủ kín toàn bộ cột trái, logo và lời chào đặt trên lớp chuyển sắc để dễ đọc. Ba ảnh dọc độ phân giải 1440 × 1920 tự chuyển mờ mỗi 6 giây, kể cả khi trỏ chuột trên ảnh hoặc điền form. Không còn khung ảnh nhỏ, chú thích, chấm chọn hoặc nút điều khiển ảnh. Ảnh được lưu cục bộ, xem [nguồn ảnh](src/assets/auth/CREDITS.md). Tự chuyển dừng khi tab bị ẩn hoặc bật giảm chuyển động; trên điện thoại phần ảnh được ẩn để ưu tiên form. Form đăng ký không hiển thị khung giải thích phân quyền; thông tin này nằm trong Hồ sơ sau đăng nhập.

MCP Client Lab và Playground gọi MCP Client C# qua API, khám phá schema và thực thi Tools thật. Read tools chạy trực tiếp; write tools yêu cầu đề nghị được lưu và xác nhận người dùng trước khi thực thi. AI Assistant lưu hội thoại và tool trace; không có key hợp lệ thì phần AI báo lỗi hoặc chưa cấu hình, còn MCP Direct Test Mode vẫn hoạt động. Model hiện chọn là `openai/gpt-oss-20b` trên GroqCloud, dùng `reasoning_effort=low` ở backend. Ngày 2026-10-30 đã kiểm chứng LLM thật → MCP C# → PostgreSQL → câu trả lời với dữ liệu mẫu 32 sản phẩm/tổng tồn 0. Trình duyệt cũng đã kiểm tra đăng nhập, model runtime và schema MCP sau khi nạp cấu hình mới.

Trong lúc gửi chat, giao diện đọc tiến độ đã lưu mỗi 2 giây và hiển thị bước thực tế: khám phá tools, gọi LLM, chạy MCP tool hoặc chờ rate limit, kèm thời gian đã chờ và nút Dừng. Rời trang cũng hủy request. Timeout trình duyệt lấy deadline backend cộng 15 giây: hiện tại là 135 giây với backend 120 giây; nếu chưa đọc được deadline thì dùng 120 + 15 giây.

Khi Groq trả lỗi tốc độ có `Retry-After` hợp lệ, backend có thể chờ và thử lại riêng lượt completion bị từ chối một lần. Không tự gửi lại toàn bộ chat, lỗi billing hoặc thao tác MCP. Kết quả MCP có cấu trúc được gửi một lần cho LLM; lịch sử/audit vẫn giữ toàn bộ envelope. Thời gian trả lời vẫn phụ thuộc provider và số lượt gọi tool.

Access token lưu trong memory/sessionStorage; refresh token nằm trong HttpOnly cookie với rotation và thu hồi phía server. Giao diện xử lý gia hạn phiên, lỗi kết nối, đăng xuất và thay đổi tài khoản giữa các tab. Không lưu mật khẩu trong browser storage.

```sh
npm run build
npm run preview
```

Build kiểm tra TypeScript và tạo `dist/`. Production cần reverse proxy HTTPS route `/api` và `/uploads` về ASP.NET Core và SPA fallback về `index.html`; Vite preview không có API proxy production.

Tests đã chuyển ra [AI-Smart-Inventory-Support](../../../AI-Smart-Inventory-Support/README.md), không còn script `npm test` trong ứng dụng. Xem [README hệ thống](../../README.md) để cấu hình database, backend, MCP Server và provider.
