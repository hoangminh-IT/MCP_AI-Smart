# Tạo dữ liệu PostgreSQL bằng C#

Đã bỏ script Python nhập kho. `SmartInventory.DatabaseSeeder` là console C#/.NET 10, dùng HttpClient đăng nhập và gọi API nghiệp vụ hiện có. API ghi PostgreSQL qua StockService, cập nhật tồn kho và stock movement theo transaction. Không ghi SQL trực tiếp hoặc sửa code ứng dụng.

## Chạy

Sao lưu DB bằng PostgreSQL/pgAdmin trước. Chạy API với cấu hình DB đích đã migrate:

```sh
dotnet run --project backend/SmartInventory.Api --launch-profile http
```

Mở terminal khác tại thư mục gốc dự án:

```sh
dotnet run --project backend/SmartInventory.DatabaseSeeder
```

Nhập tài khoản Admin/Staff hiện tại của ứng dụng và mật khẩu ẩn. Đây là tài khoản ứng dụng, không phải tài khoản PostgreSQL. Endpoint mặc định `http://localhost:5062`, đổi bằng biến môi trường `INVENTORY_API_URL` nếu cần.

## Phạm vi

- DB đã kiểm tra có 32 sản phẩm và 5 danh mục. Lần nhập tự động trước đó dừng ở HTTP 401, chưa thêm phiếu nhập.
- Console dự kiến nhập SP001–SP008 lần lượt 2, 5, 9, 10, 15, 20, 30, 50 vào MAIN (ID 1), chỉ với SKU tìm thấy đang có tổng tồn bằng 0.
- Không xóa dữ liệu, không tự tạo DB/migrate, không đổi quyền hoặc mật khẩu. Request ID cố định giúp chống nhập trùng; phản hồi replay được in rõ.
- Nếu lỗi giữa chừng, các phiếu đã thành công vẫn được giữ lại. Kiểm tra lịch sử trước khi chạy lại.
- API dùng DB nào thì dữ liệu thêm vào DB đó. Notebook chỉ đọc dữ liệu và chấp nhận tên DB hiện có. Cấu hình API, MCP và SQL cùng database/schema trước khi thực nghiệm; công cụ không tự sao chép database.

Dữ liệu mẫu được lưu thật trong PostgreSQL, không phải dữ liệu kinh doanh thu thập thực tế. Python trong notebook chỉ phục vụ thực nghiệm/biểu đồ, không tạo dữ liệu DB.
