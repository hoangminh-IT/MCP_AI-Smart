using SmartInventory.Application.Services;

namespace SmartInventory.McpServer.Tools
{
    public class ProductMcpTools
    {
        private readonly ProductService _productService;

        public ProductMcpTools(ProductService productService)
        {
            _productService = productService;
        }

        public object GetAllProducts() => _productService.GetAll();
        public object SearchProduct(string keyword) => _productService.Search(keyword);
    }
}
