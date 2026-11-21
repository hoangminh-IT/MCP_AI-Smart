# Dữ liệu thực nghiệm

## mcp_tool_intents.csv

160 câu hỏi tiếng Việt mẫu được biên soạn cho bài, gán nhãn một trong 8 read tool MCP. Đây là dữ liệu tổng hợp phục vụ minh họa, chưa phải hội thoại người dùng được thu thập và gán nhãn độc lập.

| Cột | Ý nghĩa |
| --- | --- |
| id | Mã câu hỏi duy nhất. |
| split | train / validation / test, cố định trước huấn luyện. |
| intent | Tên tool đọc MCP cần chọn. |
| text | Câu hỏi đầu vào; đây là cột duy nhất dùng tạo features. |

Mỗi lớp có 12 câu Train, 4 Validation, 4 Test, tổng cộng 96/32/32. Không có ID hoặc câu chuẩn hóa trùng giữa các split. Từ vựng và IDF chỉ fit trên Train. Các câu có chung miền ngôn ngữ và từ khóa; tách như vậy chưa chứng minh khả năng khái quát trên cách hỏi hoàn toàn mới ngoài thực tế.

## training_configurations.json

10 bộ tham số: epochs, learningRate, l2, ngrams và seed. C# đọc trực tiếp file này, cập nhật trọng số thật và ghi log; kết quả không được điền sẵn trong cấu hình.

## Dữ liệu kho

CSV [inventory_postgresql_snapshot.csv](inventory_postgresql_snapshot.csv) (kèm metadata nguồn) là 32 sản phẩm mẫu được đọc từ PostgreSQL thật trong đợt kiểm thử. Nó dùng để đối chiếu MCP/SQL, không dùng làm dataset huấn luyện mô hình chọn tool. Không coi 32 số dư tĩnh là lịch sử bán hàng để huấn luyện dự báo nhu cầu.
## mcp_read_configurations.json

10 bộ arguments của các tool đọc MCP trong lượt notebook đã lưu. Xem `observedRunId` để đối chiếu đúng log; đây là tham số gọi tool, khác với tham số Training. ID/SKU gắn với DB test của lượt đó. Khi chạy lại, notebook đọc snapshot mới để chọn ID/SKU phù hợp.
