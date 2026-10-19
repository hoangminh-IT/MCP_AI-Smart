#pragma once
#include <string>
#include <nlohmann/json.hpp>

using json = nlohmann::json;

struct Product {
    int id;
    std::string sku;
    std::string name;
    double price;
    int category_id;
    bool is_active = true;

    NLOHMANN_DEFINE_TYPE_INTRUSIVE(Product, id, sku, name, price, category_id, is_active)
};

struct CreateProductDTO {
    std::string sku;
    std::string name;
    double price;
    int category_id;

    NLOHMANN_DEFINE_TYPE_INTRUSIVE(CreateProductDTO, sku, name, price, category_id)
};

struct Category {
    int id;
    std::string name;

    NLOHMANN_DEFINE_TYPE_INTRUSIVE(Category, id, name)
};