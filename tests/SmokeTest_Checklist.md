# CHECKLIST KIỂM THỬ API & SMOKE TEST (PHẦN VIỆT SỸ)

## 1. Test API CRUD Sản phẩm & Danh mục (Task T046)
- [ ] **GET /api/categories**: Trả về danh sách danh mục (Status 200 OK).
- [ ] **GET /api/products**: Trả về danh sách sản phẩm có phân trang (Status 200 OK).
- [ ] **GET /api/products/1**: Trả về chi tiết sản phẩm ID = 1 (Status 200 OK).
- [ ] **GET /api/products/999**: Trả về lỗi không tìm thấy (Status 404 Not Found).
- [ ] **POST /api/products**: 
  - Tạo thành công sản phẩm mới với SKU hợp lệ (Status 201 Created).
  - Trả về lỗi khi tạo trùng mã SKU (Status 400 Bad Request).
- [ ] **PUT /api/products/1**: Cập nhật thông tin sản phẩm (Status 200 OK).
- [ ] **DELETE /api/products/1**: Xóa mềm sản phẩm `is_active = false` (Status 200 OK).
- [ ] **GET /api/products/search?q=Logitech**: Trả về đúng danh sách sản phẩm chứa từ khóa "Logitech".

## 2. Smoke Test Toàn Hệ Thống (Task T044)
Luồng tích hợp chính:
1. `POST /auth/login` -> Đăng nhập lấy JWT Token.
2. `POST /api/products` -> Thêm sản phẩm mới vào danh mục.
3. `GET /api/products/search` -> Tra cứu sản phẩm vừa thêm.
4. Gọi Chat AI / MCP Client hỏi "Tìm sản phẩm vừa tạo" -> AI gọi MCP Tool trả về đúng dữ liệu.