using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Common;
using SmartInventory.Application.Entities;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Warehouses;

/// <summary>Common scope validation and locking. Callers lock warehouses before products and aggregate inventory.</summary>
public static class WarehouseAccess
{
    public const int MainId = 1;
    public static async Task<Warehouse?> ReadAsync(AppDbContext database, int? id, CancellationToken cancellationToken)
    {
        if (!id.HasValue) return null;
        if (id <= 0) throw new AppValidationException("warehouseId must be positive.");
        return await database.Warehouses.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new AppNotFoundException("Warehouse was not found.");
    }

    public static async Task<IReadOnlyList<Warehouse>> LockActiveAsync(AppDbContext database, IEnumerable<int> warehouseIds,
        CancellationToken cancellationToken)
    {
        if (database.Database.CurrentTransaction is null) throw new InvalidOperationException("Warehouse locks require a transaction.");
        var ids = warehouseIds.Distinct().Order().ToArray();
        if (ids.Length == 0 || ids.Any(id => id <= 0)) throw new AppValidationException("Use positive warehouse IDs.");
        var rows = await database.Warehouses.FromSqlInterpolated(
            $"SELECT * FROM warehouses WHERE id = ANY ({ids}) ORDER BY id FOR SHARE").ToListAsync(cancellationToken);
        foreach (var row in rows) await database.Entry(row).ReloadAsync(cancellationToken);
        if (rows.Count != ids.Length) throw new AppNotFoundException("Warehouse was not found.");
        if (rows.Any(row => !row.IsActive)) throw new AppConflictException("Stock and open orders require an active warehouse.");
        return rows;
    }

    public static async Task<WarehouseInventory> LockBalanceAsync(AppDbContext database, int productId, int warehouseId,
        CancellationToken cancellationToken)
    {
        // The product/global row is already locked; lazy zero initialization cannot race another
        // stock mutation for the same product. SQL values are parameters, never concatenated.
        await database.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO warehouse_inventory (product_id, warehouse_id, quantity, updated_at) VALUES ({productId}, {warehouseId}, 0, CURRENT_TIMESTAMP) ON CONFLICT (product_id, warehouse_id) DO NOTHING", cancellationToken);
        var balance = await database.WarehouseInventories.FromSqlInterpolated(
            $"SELECT * FROM warehouse_inventory WHERE product_id = {productId} AND warehouse_id = {warehouseId} FOR UPDATE").SingleAsync(cancellationToken);
        await database.Entry(balance).ReloadAsync(cancellationToken);
        return balance;
    }
}
