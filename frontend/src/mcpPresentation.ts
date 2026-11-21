// Display text only. Tool identifiers, server descriptions and input schemas stay intact.
const toolText: Record<string, [string, string]> = {
  GetAllProducts: ['Danh sách sản phẩm', 'Xem sản phẩm đang hoạt động và số lượng tồn kho.'],
  GetProductById: ['Chi tiết sản phẩm', 'Tra cứu thông tin và tồn kho bằng ID sản phẩm.'],
  GetProductBySku: ['Tra cứu theo SKU', 'Xem sản phẩm và số lượng hiện có theo mã SKU.'],
  SearchProducts: ['Tìm sản phẩm', 'Tìm kiếm theo tên hoặc mã SKU.'],
  GetProductsByCategory: ['Sản phẩm theo danh mục', 'Lọc sản phẩm và tồn kho trong một danh mục.'],
  GetLowStockProducts: ['Sản phẩm sắp hết', 'Xem sản phẩm có tồn dưới ngưỡng đã chọn hoặc mức tối thiểu.'],
  GetOutOfStockProducts: ['Sản phẩm hết hàng', 'Xem sản phẩm đang có số lượng tồn bằng 0.'],
  GetInventorySummary: ['Tổng quan tồn kho', 'Tổng hợp số lượng, giá trị và cảnh báo tồn kho.'],
  GetStockMovementHistory: ['Lịch sử nhập xuất', 'Tra cứu giao dịch kho, có thể lọc theo sản phẩm.'],
  GetStockMovements: ['Tra cứu giao dịch kho', 'Lọc lịch sử nhập, xuất và điều chỉnh theo khoảng thời gian.'],
  GetCategorySummary: ['Tổng hợp danh mục', 'Xem số sản phẩm, lượng tồn và giá trị của từng danh mục.'],
  GetSupplierProducts: ['Hàng từ nhà cung cấp', 'Xem những sản phẩm liên kết với một nhà cung cấp.'],
  GetSuppliers: ['Danh sách nhà cung cấp', 'Xem thông tin và trạng thái nhà cung cấp.'],
  SearchSuppliers: ['Tìm nhà cung cấp', 'Tìm theo mã, tên hoặc thông tin liên hệ.'],
  GetCustomers: ['Danh sách khách hàng', 'Xem thông tin và trạng thái khách hàng.'],
  SearchCustomers: ['Tìm khách hàng', 'Tìm theo mã, tên hoặc thông tin liên hệ.'],
  GetPurchaseOrders: ['Danh sách đơn mua', 'Tra cứu đơn mua theo nhà cung cấp và trạng thái.'],
  GetPurchaseOrderById: ['Chi tiết đơn mua', 'Xem sản phẩm, chi phí và lịch sử nhận hàng của đơn mua.'],
  GetPurchaseOrderStatus: ['Trạng thái đơn mua', 'Kiểm tra trạng thái, số lượng đặt và số lượng đã nhận.'],
  GetSalesOrders: ['Danh sách đơn bán', 'Tra cứu đơn bán theo khách hàng và trạng thái.'],
  GetSalesOrderById: ['Chi tiết đơn bán', 'Xem sản phẩm, giá bán và lịch sử giao hàng của đơn bán.'],
  GetSalesOrderStatus: ['Trạng thái đơn bán', 'Kiểm tra trạng thái, số lượng đặt và số lượng đã giao.'],
  GetPurchaseSummary: ['Tổng hợp mua hàng', 'Tổng hợp đơn mua và hàng đã nhận theo khoảng thời gian.'],
  GetSalesSummary: ['Tổng hợp bán hàng', 'Tổng hợp đơn bán và hàng đã giao theo khoảng thời gian.'],
  GetWarehouses: ['Danh sách kho', 'Xem các kho đang được quản lý.'],
  CreateStockReceipt: ['Nhập hàng vào kho', 'Tăng tồn kho và lưu giao dịch nhập hàng sau khi bạn xác nhận.'],
  CreateStockIssue: ['Xuất hàng khỏi kho', 'Giảm tồn kho và lưu giao dịch xuất hàng; kiểm tra số lượng hiện có.'],
  AdjustInventory: ['Điều chỉnh sau kiểm kê', 'Đặt tồn kho về số lượng thực tế. Chức năng dành cho quản trị viên.'],
  CreatePurchaseOrder: ['Tạo đơn mua hàng', 'Lập đơn mua nháp với nhà cung cấp; tồn kho chưa thay đổi.'],
  UpdatePurchaseOrderStatus: ['Cập nhật đơn mua', 'Gửi duyệt, phê duyệt hoặc hủy đơn mua theo quyền được cấp.'],
};

export function toolPresentation(tool: { name: string; description?: string }) {
  const text = toolText[tool.name];
  return { title: text?.[0] ?? tool.name, description: text?.[1] ?? tool.description ?? 'Xem schema để tìm hiểu tham số và cách sử dụng.' };
}

export function executionStatus(status: string | undefined, failed = false) {
  const labels: Record<string, string> = { Succeeded: 'Thành công', Failed: 'Thất bại', ToolError: 'Lỗi công cụ', ProtocolError: 'Lỗi giao thức', Timeout: 'Hết thời gian', Cancelled: 'Đã dừng', AwaitingConfirmation: 'Chờ xác nhận' };
  return status ? labels[status] ?? status : failed ? 'Thất bại' : 'Thành công';
}

export function aiProviderHint(reason: string | undefined) {
  const messages: Record<string, string> = {
    'Configure Llm:BaseUrl as an HTTPS API base URL, or HTTP on loopback for local development.': 'Địa chỉ kết nối AI chưa hợp lệ. Quản trị viên cần kiểm tra cấu hình.',
    'Configure Llm:Model with a model supporting Chat Completions tool calling.': 'Chưa chọn mô hình AI hỗ trợ công cụ. Liên hệ quản trị viên để hoàn tất cấu hình.',
    'Configure Llm:ApiKey through User Secrets or environment variables. MCP Direct Test Mode remains available.': 'Trợ lý AI chưa được kích hoạt. Liên hệ quản trị viên để hoàn tất cấu hình.',
  };
  return reason ? messages[reason] ?? reason : 'Trợ lý AI chưa sẵn sàng. Liên hệ quản trị viên để kiểm tra cấu hình.';
}
