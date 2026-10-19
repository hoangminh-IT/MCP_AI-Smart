#include "core/include/Services/ProductService.h"
#include <nlohmann/json.hpp>

using json = nlohmann::json;

json McpTool_GetAllProducts(std::shared_ptr<ProductService> service, const json& args) {
    int page = args.value("page", 1);
    int limit = args.value("limit", 10);
    auto products = service->GetAll(page, limit);

    return json{
        {"content", json::array({
            {
                {"type", "text"},
                {"text", json(products).dump(2)}
            }
        })}
    };
}

json McpTool_SearchProduct(std::shared_ptr<ProductService> service, const json& args) {
    std::string query = args.value("query", "");
    auto products = service->Search(query);

    return json{
        {"content", json::array({
            {
                {"type", "text"},
                {"text", json(products).dump(2)}
            }
        })}
    };
}