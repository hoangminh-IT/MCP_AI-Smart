# Hoàn thiện thực nghiệm, giao diện và phân quyền

## 1. Phạm vi nghiệm thu

Mục tiêu là có bằng chứng cho tình huống thử, bộ tham số, kết quả so sánh, nhận xét và cải tiến; đồng thời thống nhất giao diện và kiểm tra quyền Admin/Staff/Customer. Không suy ra “không còn bất kỳ lỗi nào” từ một lần build hoặc một bộ thử.

Đợt hoàn thiện ngày **21/11/2026**, múi giờ Việt Nam. Trạng thái và số liệu cuối cùng xem [báo cáo thực nghiệm](../experiments/reports/BAO-CAO-THUC-NGHIEM.md) và các artifact được liên kết trong báo cáo.

## 2. Nguồn bằng chứng đang có

| Nguồn | Vai trò |
| --- | --- |
| [Verification C#](../experiments/Verification/Program.cs) | Tạo DB test riêng, migrate, tạo tài khoản, seed dữ liệu và gọi API/MCP thật; tự dọn DB của lần chạy. |
| [Notebook](../experiments/MCP_CSharp_Inventory_Experiments.ipynb) | Đọc snapshot SQL, thử 10 cấu hình qua MCP, so sánh ID/tồn/số lượng và lưu bảng, biểu đồ. |
| [Execution log](../experiments/results/verification.json) | Input/output thực, HTTP mong đợi/thực tế, execution ID và trạng thái toàn suite. |
| [Browser QA](../experiments/results/browser/browser-results.json) | Route, kích thước màn hình, theme, chuyển hướng theo vai trò và runtime exceptions. |
| [Ảnh desktop/mobile](../experiments/results/browser) | Bằng chứng hiển thị trên dữ liệu mẫu của DB test. |
| [Hướng dẫn tái hiện](../experiments/Verification/README.md) | Điều kiện môi trường và lệnh chạy. |

Các số liệu trong kế hoạch được đối chiếu với log được lưu trong `experiments/results/` của dự án.

## 3. Tình huống và bộ tham số

| Nhóm | Ca đã triển khai | Tiêu chí |
| --- | --- | --- |
| Đọc dữ liệu | PAGE-5/10/20/100 | Trả đúng số dòng và totalCount = 32. |
| Ngưỡng tồn | LOW-0/5/20, OUT | Đối chiếu điều kiện quantity < threshold và quantity = 0 với PostgreSQL. |
| Nhận diện | ID, SKU | Cùng trả đúng sản phẩm đã chọn. |
| Validation | VALID-PAGE | page = 0 phải báo lỗi, không được coi là dữ liệu thành công. |
| Phân quyền API | ANON; Admin/Staff/Customer × products/users/mcp | HTTP 401/403/200 đúng với danh tính và quyền. |
| Ghi có xác nhận | PREPARE → CONFIRM | Chuẩn bị không đổi tồn; xác nhận mới thực thi. |
| Chạy lại | REPLAY | Cùng intent không ghi tồn hoặc movement lần thứ hai. |
| Xuất kho | ISSUE, OVERDRAW | Xuất đúng; xuất vượt tồn bị chặn và không đổi số dư. |
| Hủy | CANCEL | Hủy intent không đổi tồn. |
| LLM | AI-LIVE | Provider thật chọn GetInventorySummary; trace MCP và số tổng phải khớp DB test. |
| Giao diện | 21 route × desktop sáng/mobile tối; 8 route Staff/Customer | Render được, không tràn ngang toàn trang, không runtime exception, chuyển hướng đúng. |

Đầu vào đầy đủ nằm trong code và JSON log, không chỉ mô tả bằng lời. Dữ liệu sản phẩm là dữ liệu mẫu lưu thật trong PostgreSQL, không phải dữ liệu kinh doanh thu thập ngoài thực tế.

## 4. Kết quả, so sánh và nhận xét

- [x] Có mã ca, mục đích, bộ tham số và mong đợi.
- [x] Có bộ thử C# thực thi API → MCP C# → PostgreSQL thật.
- [x] Có kiểm tra số dư trước/sau, movement và replay.
- [x] Có ma trận quyền API và chuyển hướng giao diện.
- [x] Có notebook chạy được trên DB riêng qua bộ QA tự động.
- [x] Có ảnh desktop sáng và mobile tối.
- [x] Có hướng dẫn chạy lại bằng C#, không dùng Python để tạo dữ liệu DB.
- [x] Chốt lượt chạy kết hợp: 43 lời gọi kiểm tra đạt, 50 kiểm tra browser đạt, notebook không lỗi/có PNG nhúng và LLM gọi GetInventorySummary thành công.
- [x] Có báo cáo, bảng CSV, snapshot SQL, biểu đồ và slide HTML offline cùng số liệu.

Báo cáo phải phân biệt giai đoạn đọc trước các thao tác ghi với snapshot sau ghi. Không gộp số lượng khác nhau giữa các giai đoạn thành một kết quả; thời gian chỉ là quan sát một lần, chưa phải benchmark.

## 5. Chuẩn giao diện đã áp dụng

| Thành phần | Quy chuẩn |
| --- | --- |
| Nội dung trang | Cùng lề workspace, chiều rộng nội dung tối đa 1520px; tiêu đề h1, mô tả, vùng thao tác và nội dung rõ thứ tự. |
| Chữ | Theme body 16px/14px, nhãn thao tác chính 14px, metadata nhỏ hơn; line-height đủ cho tiếng Việt. |
| Tiêu đề và nút | Xuống dòng theo màn hình, giới hạn max-width, không ép nút ra ngoài vùng nội dung. |
| Bảng | Số liệu dùng tabular-nums, bảng rộng cuộn trong container; bảng dùng chung nhận focus bàn phím. |
| Phân trang | Vẫn có đường quay về trang trước khi trang hiện tại rỗng nhưng totalCount còn lớn hơn 0. |
| Thông báo | Nội dung lỗi dài có thể ngắt dòng; không che lỗi để tạo cảm giác thành công. |
| Dialog | Giới hạn theo viewport; vùng nút cho phép xuống dòng. |
| Responsive | Kiểm tra 1440×1000 và 390×844; dữ liệu bảng có thể cuộn ngang bên trong. |
| Theme | Desktop light và mobile dark; tương phản, trạng thái và nội dung được kiểm tra qua ảnh. |
| Trợ năng cơ bản | Focus nhìn thấy, skip link, bảng cuộn bằng bàn phím và reduced-motion được giữ. |

Không thay thư viện giao diện hoặc viết lại toàn frontend. Chỉ chỉnh các thành phần dùng chung và router phục vụ lỗi/phạm vi xác định được.

## 6. Phân quyền

| Chức năng | Admin | Staff | Customer |
| --- | --- | --- | --- |
| Hồ sơ cá nhân | Có | Có | Có |
| Đọc sản phẩm/tồn và MCP | Có | Có | Không |
| Quản lý người dùng và audit | Có | Không | Không |
| Nhập/xuất kho | Theo nghiệp vụ | Theo nghiệp vụ | Không |
| Điều chỉnh tồn | Có | Không | Không |
| Sửa cấu hình hệ thống | Có | Không | Không |

API và service vẫn là nơi quyết định quyền. Frontend bổ sung AdminArea cho `/users`, `/audit`; StaffArea chỉ cho Admin/Staff vào nghiệp vụ kho. Ẩn menu không thay thế kiểm tra backend.

Bộ thử hiện xác minh HTTP cho products/users/mcp và chuyển hướng các route tương ứng. Các hàng khác được đối chiếu code, chưa đồng nghĩa tất cả endpoint và biến thể payload đã được kiểm thử động.

## 7. Cải tiến và bằng chứng

| Vấn đề | Thay đổi | Cách kiểm tra |
| --- | --- | --- |
| Bằng chứng cũ nằm ngoài workspace | Thêm bộ thử C# tự tái hiện trên DB riêng | Execution log và `passed` toàn suite. |
| Notebook thiếu output/khó chạy theo thứ tự | Kiểm tra điều kiện từng bước; hỗ trợ QA tự động; lưu output | Mọi ô code có execution count, không có error output. |
| Chữ nhỏ, nút có thể chật | Chuẩn hóa typography, heading/actions và dialog | Ảnh desktop/mobile và kiểm tra overflow. |
| Route admin chỉ ẩn menu hoặc kiểm tra trong trang | Thêm AdminArea tại router | Staff/Customer mở URL trực tiếp phải chuyển hướng. |
| Trang bảng rỗng mất phân trang | Tách phân trang khỏi điều kiện rows.length | Kiểm tra code, build và các trang dùng chung. |
| QA mở nhiều trang gặp 429 | Tăng hạn mức chỉ trong process test | Cấu hình mặc định ứng dụng không thay đổi. |

Không có ảnh chụp trước thay đổi trong đợt này. So sánh trước/sau dựa trên diff và hành vi được mô tả; ảnh trong báo cáo là trạng thái sau thay đổi.

## 8. Cách tái hiện và giới hạn

Chạy lệnh từ thư mục gốc theo [Verification README](../experiments/Verification/README.md). Khi chạy cả browser/notebook, cần Chrome local và Python kernel có đủ thư viện. Chế độ LLM chỉ chạy sau chấp thuận gửi dữ liệu mẫu tới provider đang cấu hình.

- Giữ nguyên migrations, dữ liệu upload và thay đổi đang có của người dùng.
- Không reset DB development, đổi mật khẩu hoặc sửa quyền tài khoản hiện tại.
- Không ghi secrets/JWT vào log, notebook hoặc Git.
- DB kiểm thử được đặt tên ngẫu nhiên và dọn đúng tài nguyên do harness tạo.
- Chưa chứng nhận mọi kích thước màn hình, mọi trình duyệt, tải đồng thời, deployment production hoặc độ chính xác LLM trên mọi câu hỏi.
- File báo cáo và slide phải ghi đúng phạm vi; không đánh dấu hoàn thành kiểm tra chưa chạy.

## 9. Bàn giao

- [Báo cáo thực nghiệm](../experiments/reports/BAO-CAO-THUC-NGHIEM.md).
- [Slide HTML offline](../experiments/reports/PRESENTATION.html): mở trong trình duyệt; dùng nút Trước/Tiếp hoặc phím trái/phải. Có thể in PDF bằng trình duyệt.
- [Notebook đã chạy](../experiments/MCP_CSharp_Inventory_Experiments.ipynb).
- [Hướng dẫn bộ kiểm chứng](../experiments/Verification/README.md).

Các file frontend chỉnh trong đợt này: `main.tsx` (guard), `appearance.tsx` (theme), `shared.tsx` (heading), `shell.tsx` (menu), `styles.css` (responsive) và `components/ServerDataTable.tsx` (bảng/phân trang). Code nghiệp vụ và migrations backend không bị chỉnh trong đợt này.

Nghiệm thu đạt trong phạm vi đã ghi nhận. Không dùng con số “100% không lỗi” cho toàn hệ thống; các giới hạn tại mục 8 vẫn phải được giữ trong báo cáo/thuyết trình.

## 10. Bổ sung yêu cầu Training/Test

- [x] Đổi tên notebook thành `MCP_CSharp_Inventory_Experiments.ipynb`.
- [x] Dataset 160 câu mẫu, 8 nhãn tool, tách cố định 96 Train / 32 Validation / 32 Test; nguồn dữ liệu được ghi rõ.
- [x] Huấn luyện classifier TF-IDF + softmax bằng C# với 10 cấu hình, không fine-tune LLM.
- [x] Log 591 bản ghi epoch, 10 sự kiện đánh giá Test, 320 dự đoán; model, split ID và hash dữ liệu được lưu.
- [x] Khóa cấu hình bằng Validation trước Test; giữ lỗi phân loại trong báo cáo, không chọn lại bằng Test.
- [x] Notebook chạy 11/11 ô, không lỗi, hai biểu đồ PNG nhúng; tám dự đoán cố định gọi MCP/PostgreSQL thật thành công.
- [x] Báo cáo và slide được cập nhật cùng kết quả; [báo cáo Training/Test](../experiments/reports/TRAINING-TEST-REPORT.md) chứa liên kết tới đúng run-id.

Đây là mô hình phụ minh họa lớp chọn tool, chưa triển khai trong AI Assistant. Dataset câu mẫu nhỏ và chưa được đánh giá độc lập trên người dùng thực tế; không khẳng định accuracy production hoặc gọi đây là huấn luyện LLM.
