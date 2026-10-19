#include "core/include/Services/ProductService.h"
#include <algorithm>
#include <cctype>

static std::string ToLower(std::string str) {
    std::transform(str.begin(), str.end(), str.begin(), [](unsigned char c){ return std::tolower(c); });
    return str;
}

ProductService::ProductService() {
    categories_ = { {1, "Linh kiện"}, {2, "Thiết bị ngoại vi"} };
    next_category_id_ = 3;

    products_ = {
        {1, "LOG-MX3", "Chuột Logitech MX Master 3S", 2490000.0, 2, true},
        {2, "KEY-K380", "Bàn phím Bluetooth Logitech K380", 690000.0, 2, true},
        {3, "RAM-16G", "RAM Corsair Vengeance 16GB DDR4", 1200000.0, 1, true}
    };
    next_product_id_ = 4;
}

std::vector<Product> ProductService::GetAll(int page, int limit) {
    std::lock_guard<std::mutex> lock(mutex_);
    std::vector<Product> result;
    int start = (page - 1) * limit;
    
    for (size_t i = start; i < products_.size() && result.size() < static_cast<size_t>(limit); ++i) {
        if (products_[i].is_active) {
            result.push_back(products_[i]);
        }
    }
    return result;
}

std::optional<Product> ProductService::GetById(int id) {
    std::lock_guard<std::mutex> lock(mutex_);
    for (const auto& p : products_) {
        if (p.id == id && p.is_active) return p;
    }
    return std::nullopt;
}

std::vector<Product> ProductService::Search(const std::string& query) {
    std::lock_guard<std::mutex> lock(mutex_);
    std::vector<Product> result;
    std::string q = ToLower(query);

    for (const auto& p : products_) {
        if (!p.is_active) continue;
        if (ToLower(p.name).find(q) != std::string::npos || ToLower(p.sku).find(q) != std::string::npos) {
            result.push_back(p);
        }
    }
    return result;
}

bool ProductService::Create(const CreateProductDTO& dto, std::string& error_msg) {
    std::lock_guard<std::mutex> lock(mutex_);
    
    for (const auto& p : products_) {
        if (p.sku == dto.sku && p.is_active) {
            error_msg = "Mã SKU đã tồn tại trong hệ thống";
            return false;
        }
    }

    Product new_prod{
        next_product_id_++,
        dto.sku,
        dto.name,
        dto.price,
        dto.category_id,
        true
    };
    products_.push_back(new_prod);
    return true;
}

bool ProductService::Update(int id, const CreateProductDTO& dto, std::string& error_msg) {
    std::lock_guard<std::mutex> lock(mutex_);
    for (auto& p : products_) {
        if (p.id == id && p.is_active) {
            p.sku = dto.sku;
            p.name = dto.name;
            p.price = dto.price;
            p.category_id = dto.category_id;
            return true;
        }
    }
    error_msg = "Không tìm thấy sản phẩm";
    return false;
}

bool ProductService::SoftDelete(int id) {
    std::lock_guard<std::mutex> lock(mutex_);
    for (auto& p : products_) {
        if (p.id == id && p.is_active) {
            p.is_active = false;
            return true;
        }
    }
    return false;
}

std::vector<Category> ProductService::GetAllCategories() {
    std::lock_guard<std::mutex> lock(mutex_);
    return categories_;
}

bool ProductService::CreateCategory(const std::string& name) {
    std::lock_guard<std::mutex> lock(mutex_);
    categories_.push_back({next_category_id_++, name});
    return true;
}