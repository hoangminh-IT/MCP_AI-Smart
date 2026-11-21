# Log và bằng chứng thực nghiệm

| Vị trí | Nội dung |
| --- | --- |
| [training/](training/) | Một lượt Training/Test hoàn chỉnh được dùng trong báo cáo; `latest.json` chỉ tới model mới nhất. |
| [notebook/](notebook/) | Snapshot SQL, 10 cấu hình và execution log của phần MCP trong notebook hiện tại. |
| [verification.json](verification.json) | Đợt nghiệm thu MCP/LLM đã được chấp thuận; giữ nguyên thời điểm và output. |
| [http-comparison.csv](http-comparison.csv), [parameter-comparison.csv](parameter-comparison.csv) | Kết quả HTTP và 10 cấu hình MCP của đợt nghiệm thu gốc. |
| `notebook-real_*` | Snapshot/CSV/log của notebook ở đợt nghiệm thu gốc; có run ID riêng, khác lượt trong `notebook/`. |
| [browser/](browser/) | Ảnh desktop/mobile và kết quả kiểm tra route, quyền, trường mật khẩu, đăng ký/khôi phục. |
| `local-verification.json`, `password-*-results.json`, `password-*-suite.json` | Bằng chứng kiểm tra API, chính sách mật khẩu và SMTP local. |
| [environment.json](environment.json) | Phiên bản môi trường đã dùng khi kiểm tra bộ thực nghiệm. |

Lượt Training trong báo cáo: `20261121T1206152520820Z`, có 591 bản ghi epoch, 10 sự kiện Test, 320 dự đoán và tám lần gọi MCP từ dự đoán. Lượt notebook tương ứng: `20261121T120616009173Z`, có 10 cấu hình và 20 lần gọi MCP để đối chiếu toàn bộ trang.

Không trộn thời gian hoặc execution ID giữa các lượt. Training/Test thực thi thật trên dữ liệu câu hỏi tổng hợp; các kết quả MCP dùng dữ liệu mẫu trong PostgreSQL kiểm thử. Xem [báo cáo Training/Test](../reports/TRAINING-TEST-REPORT.md) và [báo cáo MCP](../reports/BAO-CAO-THUC-NGHIEM.md) để biết phạm vi và giới hạn.
