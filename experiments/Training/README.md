# Training/Validation/Test bộ phân loại chọn tool MCP — C#

Mô hình bổ sung cho bài MCP C# SDK: đầu vào là câu hỏi tiếng Việt, đầu ra là một trong 8 read tool MCP. Đây là huấn luyện bộ phân loại mới bằng C#, không phải fine-tuning LLM và không phải huấn luyện giao thức MCP. Chưa tích hợp mô hình này vào AI Assistant production.

## Chạy từ thư mục gốc dự án

```sh
dotnet run --project experiments/Training
```

Thử model đã lưu:

```sh
dotnet run --project experiments/Training -- --predict "Sản phẩm nào sắp hết hàng"
```

Không cần LLM key, SMTP hoặc DB cho Training. Notebook điều phối C# và vẽ biểu đồ bằng pandas/matplotlib; kiểm thử MCP/SQL ở phần sau vẫn cần môi trường API/PostgreSQL hoặc runner C#.

## Thuật toán và cách tránh dùng nhầm Test

1. Đọc dataset cố định 96 Train / 32 Validation / 32 Test; kiểm tra ID và câu chuẩn hóa không trùng, cân bằng 8 lớp.
2. Chuẩn hóa chữ thường/bỏ dấu tiếng Việt; tạo unigram hoặc unigram+bigram. Chỉ dùng văn bản Train để tạo từ vựng và IDF.
3. Vector TF-IDF với TF = 1 + log(count), IDF trơn = log((N + 1)/(df + 1)) + 1, chuẩn hóa L2 từng vector.
4. Huấn luyện softmax regression đa lớp bằng full-batch gradient descent với L2 trên trọng số, không phạt bias. Loss ghi nhận là cross-entropy dữ liệu, chưa cộng phần phạt L2.
5. Ghi Train/Validation mỗi epoch, gồm trạng thái trước cập nhật ở epoch 0. Seed khởi tạo 42; không có lựa chọn ngẫu nhiên dựa trên điểm Test.
6. Chọn cấu hình theo Validation macro-F1, sau đó Validation loss, trước khi đánh giá bất kỳ câu Test nào. Ghi `selection.json` trước Test.
7. Đánh giá cả 10 cấu hình trên Test để lập bảng so sánh, giữ nguyên cấu hình đã chọn; ghi accuracy, macro-F1, confusion matrix và dự đoán từng mẫu.
8. Lưu/nạp lại model và kiểm tra xác suất dự đoán giữ nguyên; kiểm tra phân phối xác suất hữu hạn và loss Train giảm.

Các cấu hình được chạy từ `../data/training_configurations.json`, không hardcode kết quả. Tiêu chí, split ID, hash dataset/config và model parameters được lưu để đối chiếu.

## Output của mỗi lượt

`../results/training/<run-id>/` chứa:

- `dataset_manifest.json`: nguồn dữ liệu, ID theo split, SHA-256 và các nhãn.
- `training_log.csv` / `training_log.jsonl`: log Training/Validation từng epoch.
- `test_log.jsonl`: 10 sự kiện đánh giá Test thật, kèm confusion matrix.
- `test_predictions.csv`: 320 dự đoán (32 Test × 10 cấu hình).
- `experiment_results.csv`: tham số, loss/accuracy/F1 của từng cấu hình.
- `selection.json`, `selected_model.json`, `summary.json`: lựa chọn trước Test, model và kiểm tra toàn pipeline.
- `training_comparison.png`: biểu đồ được tạo khi chạy notebook.
- `routing_mcp_results.csv` / `routing_mcp_test_log.jsonl`: chỉ xuất khi notebook thực thi các dự đoán qua MCP thật.

`../results/training/latest.json` trỏ tới lượt Training mới nhất. Các thư mục lượt trước được giữ nguyên. Nếu cần log có cả MCP, dùng đúng run-id được in trong notebook, không lấy nhầm một lượt CLI chỉ huấn luyện.

## Giới hạn cần trình bày

Dataset là câu mẫu nhỏ do trợ lý biên soạn, chưa có đánh giá độc lập trên người dùng thật. Test chỉ 32 câu; một lỗi tương đương 3,125 điểm phần trăm accuracy. Baseline là luôn chọn một lớp chiếm đa số (các lớp cân bằng). Softmax score chưa được calibration. Chưa xử lý câu ngoài phạm vi, arguments hoặc thao tác ghi; backend vẫn phải kiểm tra quyền.

Một cấu hình khác có Test cao hơn không phải lý do đổi cấu hình được chọn sau khi xem Test. Cần dữ liệu và tiêu chí đánh giá độc lập để kết luận mô hình tốt hơn LLM hoặc phù hợp triển khai thực tế.
