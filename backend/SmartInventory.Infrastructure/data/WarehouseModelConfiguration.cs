using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Entities;

namespace SmartInventory.Infrastructure.Data;

internal static class WarehouseModelConfiguration
{
    internal static void ConfigureWarehouses(this ModelBuilder model)
    {
        model.Entity<Warehouse>(warehouse =>
        {
            warehouse.Property(item => item.Code).HasMaxLength(40);
            warehouse.Property(item => item.Name).HasMaxLength(200);
            warehouse.Property(item => item.Address).HasMaxLength(1000);
            warehouse.Property(item => item.IsActive).HasDefaultValue(true);
            warehouse.Property(item => item.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            warehouse.Property(item => item.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            warehouse.HasIndex(item => item.Code).IsUnique();
            warehouse.HasIndex(item => new { item.IsActive, item.Name });
            warehouse.HasIndex(item => item.IsDefault).IsUnique().HasFilter("is_default");
            warehouse.ToTable("warehouses", table => table.HasCheckConstraint("CK_warehouses_default",
                "(id = 1 AND is_default AND code = 'MAIN' AND is_active) OR (id <> 1 AND NOT is_default)"));
        });
        model.Entity<WarehouseInventory>(balance =>
        {
            balance.HasKey(item => new { item.ProductId, item.WarehouseId });
            balance.HasOne<Product>().WithMany().HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Restrict);
            balance.HasOne<Warehouse>().WithMany().HasForeignKey(item => item.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            balance.Property(item => item.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            balance.HasIndex(item => new { item.WarehouseId, item.ProductId });
            balance.ToTable("warehouse_inventory", table => table.HasCheckConstraint("CK_warehouse_inventory_quantity", "quantity >= 0"));
        });
        model.Entity<StockMovement>(movement =>
        {
            movement.Property(item => item.WarehouseId).HasDefaultValue(1);
            movement.HasOne<Warehouse>().WithMany().HasForeignKey(item => item.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            movement.HasIndex(item => new { item.WarehouseId, item.CreatedAt });
            movement.ToTable("stock_movements", table =>
            {
                table.HasCheckConstraint("CK_stock_movements_warehouse_before", "warehouse_quantity_before IS NULL OR warehouse_quantity_before >= 0");
                table.HasCheckConstraint("CK_stock_movements_warehouse_after", "warehouse_quantity_after IS NULL OR warehouse_quantity_after >= 0");
            });
        });
        model.Entity<PurchaseOrder>(order =>
        {
            order.Property(item => item.WarehouseId).HasDefaultValue(1);
            order.HasOne<Warehouse>().WithMany().HasForeignKey(item => item.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            order.HasIndex(item => new { item.WarehouseId, item.Status });
        });
        model.Entity<SalesOrder>(order =>
        {
            order.Property(item => item.WarehouseId).HasDefaultValue(1);
            order.HasOne<Warehouse>().WithMany().HasForeignKey(item => item.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            order.HasIndex(item => new { item.WarehouseId, item.Status });
        });
        model.Entity<StockTransfer>(transfer =>
        {
            transfer.Property(item => item.Id).ValueGeneratedNever();
            transfer.Property(item => item.Reason).HasMaxLength(1000);
            transfer.Property(item => item.PayloadFingerprint).HasMaxLength(64);
            transfer.HasIndex(item => item.RequestId).IsUnique();
            transfer.HasIndex(item => new { item.SourceWarehouseId, item.CreatedAt });
            transfer.HasIndex(item => new { item.DestinationWarehouseId, item.CreatedAt });
            transfer.HasOne<Product>().WithMany().HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Restrict);
            transfer.HasOne<User>().WithMany().HasForeignKey(item => item.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            transfer.HasOne<Warehouse>().WithMany().HasForeignKey(item => item.SourceWarehouseId).OnDelete(DeleteBehavior.Restrict);
            transfer.HasOne<Warehouse>().WithMany().HasForeignKey(item => item.DestinationWarehouseId).OnDelete(DeleteBehavior.Restrict);
            transfer.ToTable("stock_transfers", table =>
            {
                table.HasCheckConstraint("CK_stock_transfers_quantity", "quantity > 0");
                table.HasCheckConstraint("CK_stock_transfers_warehouses", "source_warehouse_id <> destination_warehouse_id");
                table.HasCheckConstraint("CK_stock_transfers_fingerprint", "payload_fingerprint ~ '^[0-9A-F]{64}$'");
            });
        });
    }
}
