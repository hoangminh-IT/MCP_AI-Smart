using SmartInventory.Application.Models;

namespace SmartInventory.Application.Services
{
    public class ProductService
    {
        private readonly List<Product> _products = new()
        {
            new Product { Id = 1, Name = "Laptop Dell XPS 15", Category = "Electronics", Price = 1500, Quantity = 10 },
            new Product { Id = 2, Name = "Mouse Logitech MX Master 3S", Category = "Accessories", Price = 99, Quantity = 50 }
        };

        public List<Product> GetAll() => _products;
        public Product? GetById(int id) => _products.FirstOrDefault(p => p.Id == id);
        public List<Product> Search(string keyword) =>
            _products.Where(p => p.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                                p.Category.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

        public Product Add(Product product)
        {
            product.Id = _products.Count > 0 ? _products.Max(p => p.Id) + 1 : 1;
            _products.Add(product);
            return product;
        }

        public bool Update(int id, Product updatedProduct)
        {
            var existing = GetById(id);
            if (existing == null) return false;
            existing.Name = updatedProduct.Name;
            existing.Category = updatedProduct.Category;
            existing.Price = updatedProduct.Price;
            existing.Quantity = updatedProduct.Quantity;
            return true;
        }

        public bool Delete(int id)
        {
            var product = GetById(id);
            if (product == null) return false;
            return _products.Remove(product);
        }
    }
}
