#pragma once
#include "core/include/Models/Product.h"
#include <vector>
#include <optional>
#include <string>
#include <mutex>

class ProductService {
private:
    std::vector<Product> products_;
    std::vector<Category> categories_;
    int next_product_id_ = 1;
    int next_category_id_ = 1;
    std::mutex mutex_;

public:
    ProductService();

    std::vector<Product> GetAll(int page = 1, int limit = 10);
    std::optional<Product> GetById(int id);
    std::vector<Product> Search(const std::string& query);
    bool Create(const CreateProductDTO& dto, std::string& error_msg);
    bool Update(int id, const CreateProductDTO& dto, std::string& error_msg);
    bool SoftDelete(int id);

    std::vector<Category> GetAllCategories();
    bool CreateCategory(const std::string& name);
};