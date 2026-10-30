using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;
using SmartInventory.Application.Mcp;
using SmartInventory.Infrastructure.Data;
using SmartInventory.Infrastructure.Warehouses;

namespace SmartInventory.Infrastructure.Mcp;

public sealed class McpBusinessReadService(AppDbContext database) : IMcpBusinessReadService
{
    public async Task<PagedResult<SupplierProductDto>> GetSupplierProductsAsync(int supplierId, int page, int pageSize,
        CancellationToken cancellationToken, int? warehouseId = null)
    {
        var warehouse = await WarehouseAccess.ReadAsync(database, warehouseId, cancellationToken);
        if (supplierId <= 0 || page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new AppValidationException("supplierId and page must be positive; pageSize must be between 1 and 100.");
        if (!await database.Suppliers.AsNoTracking().AnyAsync(supplier => supplier.Id == supplierId, cancellationToken))
            throw new AppNotFoundException("Supplier was not found.");
        var history = from receipt in database.PurchaseOrderReceipts.AsNoTracking()
            join order in database.PurchaseOrders.AsNoTracking() on receipt.PurchaseOrderId equals order.Id
            join line in database.PurchaseOrderReceiptItems.AsNoTracking() on receipt.Id equals line.ReceiptId
            join orderedLine in database.PurchaseOrderItems.AsNoTracking() on line.PurchaseOrderItemId equals orderedLine.Id
            join product in database.Products.AsNoTracking() on line.ProductId equals product.Id
            where order.SupplierId == supplierId && (!warehouseId.HasValue || order.WarehouseId == warehouseId.Value)
            group new { receipt, line, orderedLine } by new { product.Id, product.Sku, product.Name, product.IsActive } into grouped
            select new
            {
                ProductId = grouped.Key.Id, grouped.Key.Sku, ProductName = grouped.Key.Name, grouped.Key.IsActive,
                ReceivedQuantity = grouped.Sum(item => (long)item.line.Quantity),
                ReceivedCost = grouped.Sum(item => item.orderedLine.UnitCost * item.line.Quantity),
                LastReceivedAt = grouped.Max(item => item.receipt.CreatedAt)
            };
        var count = await history.CountAsync(cancellationToken);
        var items = await history.OrderBy(item => item.ProductName).ThenBy(item => item.ProductId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new SupplierProductDto(item.ProductId, item.Sku, item.ProductName, item.IsActive,
                item.ReceivedQuantity, item.ReceivedCost, item.LastReceivedAt, warehouseId, warehouse == null ? null : warehouse.Code, warehouse == null ? null : warehouse.Name)).ToListAsync(cancellationToken);
        return new(items, count, page, pageSize);
    }
}
