#include "crow.h"
#include "core/include/Services/ProductService.h"

void RegisterProductRoutes(crow::SimpleApp& app, std::shared_ptr<ProductService> productService) {

    CROW_ROUTE(app, "/api/products").methods(crow::HTTPMethod::GET)
    ([productService](const crow::request& req) {
        int page = req.url_params.get("page") ? std::stoi(req.url_params.get("page")) : 1;
        int limit = req.url_params.get("limit") ? std::stoi(req.url_params.get("limit")) : 10;
        
        auto products = productService->GetAll(page, limit);
        return crow::response(200, json(products).dump());
    });

    CROW_ROUTE(app, "/api/products/search").methods(crow::HTTPMethod::GET)
    ([productService](const crow::request& req) {
        char* q = req.url_params.get("q");
        std::string query = q ? q : "";
        auto results = productService->Search(query);
        return crow::response(200, json(results).dump());
    });

    CROW_ROUTE(app, "/api/products/<int>").methods(crow::HTTPMethod::GET)
    ([productService](int id) {
        auto prod = productService->GetById(id);
        if (!prod.has_value()) {
            return crow::response(404, json({{"error", "Product not found"}}).dump());
        }
        return crow::response(200, json(prod.value()).dump());
    });

    CROW_ROUTE(app, "/api/products").methods(crow::HTTPMethod::POST)
    ([productService](const crow::request& req) {
        try {
            auto body = json::parse(req.body);
            CreateProductDTO dto = body.get<CreateProductDTO>();
            
            std::string error_msg;
            if (!productService->Create(dto, error_msg)) {
                return crow::response(400, json({{"error", error_msg}}).dump());
            }
            return crow::response(201, json({{"message", "Created successfully"}}).dump());
        } catch (...) {
            return crow::response(400, json({{"error", "Invalid JSON format"}}).dump());
        }
    });

    CROW_ROUTE(app, "/api/products/<int>").methods(crow::HTTPMethod::PUT)
    ([productService](const crow::request& req, int id) {
        try {
            auto body = json::parse(req.body);
            CreateProductDTO dto = body.get<CreateProductDTO>();
            
            std::string error_msg;
            if (!productService->Update(id, dto, error_msg)) {
                return crow::response(400, json({{"error", error_msg}}).dump());
            }
            return crow::response(200, json({{"message", "Updated successfully"}}).dump());
        } catch (...) {
            return crow::response(400, json({{"error", "Invalid JSON format"}}).dump());
        }
    });

    CROW_ROUTE(app, "/api/products/<int>").methods(crow::HTTPMethod::DELETE)
    ([productService](int id) {
        if (!productService->SoftDelete(id)) {
            return crow::response(404, json({{"error", "Product not found"}}).dump());
        }
        return crow::response(200, json({{"message", "Deleted successfully"}}).dump());
    });

    CROW_ROUTE(app, "/api/categories").methods(crow::HTTPMethod::GET)
    ([productService]() {
        auto categories = productService->GetAllCategories();
        return crow::response(200, json(categories).dump());
    });
}