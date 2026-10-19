# NHẬT KÝ DEMO MCP TOOLS & CÂU HỎI AI (TASK T065)

## 1. Mẫu 3 Câu Hỏi Demo AI Nghiệp Vụ Sản Phẩm:
1. **Câu hỏi 1:** "Hệ thống đang có những danh mục sản phẩm nào?"
   - **MCP Tool dự kiến gọi:** `GetAllCategories`
2. **Câu hỏi 2:** "Tìm cho tôi các sản phẩm của thương hiệu Logitech trong kho."
   - **MCP Tool dự kiến gọi:** `SearchProduct` (args: `{"query": "Logitech"}`)
3. **Câu hỏi 3:** "Liệt kê danh sách 5 sản phẩm đầu tiên."
   - **MCP Tool dự kiến gọi:** `GetAllProducts` (args: `{"page": 1, "limit": 5}`)

## 2. Nhật Ký Log Tool Call (Minh Chứng):
```json
[
  {
    "tool_name": "SearchProduct",
    "arguments": { "query": "LOG-MX3" },
    "response_status": "SUCCESS",
    "result_count": 1
  }
]