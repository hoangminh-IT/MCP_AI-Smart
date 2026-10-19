# Smart Inventory – Frontend (Hữu Thịnh)

React + TypeScript + Material UI + React Router + Vite.

## Chạy
```bash
npm install
npm run dev        # http://localhost:5173
```

## Dữ liệu giả / backend thật
- `.env` có `VITE_USE_MOCK=true`: chạy bằng dữ liệu giả (backend chưa có API products/inventory).
- Khi backend sẵn sàng: đổi thành `VITE_USE_MOCK=false`, chạy backend bằng
  `dotnet run --launch-profile https` (cổng 7173). Vite tự proxy `/api` và `/auth`, không cần CORS khi dev.
  Backend chạy cổng khác thì đặt `VITE_PROXY_TARGET`.

## Tiến độ
- [x] T023 Khởi tạo React/TS/MUI/router
- [x] T025 Layout chung (Sidebar, Navbar, 404, loading)
- [x] T024 Dashboard 4 thẻ (tính từ API)
- [x] T026 Bảng sản phẩm
- [x] T027 Form thêm/sửa có validation
- [x] T028 Tìm kiếm + ẩn có xác nhận
- [ ] T053–T057, T063 (Ngày 2)

## Giả định cần chốt với TV1/TV2 (T006)
Sửa trong `src/api/products.ts` nếu khác:
- `GET /api/products?search=&page=1&pageSize=10` → `{ items, total }` (hoặc mảng thuần)
- `POST /api/products`, `PUT /api/products/{id}` body `{ categoryId, sku, name, price, minStock }`
- `DELETE /api/products/{id}` → ẩn mềm
- `GET /api/categories`, `GET /api/inventory` → `[{ productId, quantity }]`
