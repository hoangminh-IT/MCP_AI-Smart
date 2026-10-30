using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Entities;
using SmartInventory.Application.Sales;

namespace SmartInventory.Infrastructure.Data;

internal static class SalesOrderModelConfiguration
{
    internal static void ConfigureSalesOrders(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SalesOrder>(order =>
        {
            order.Property(item => item.Code).HasMaxLength(40);
            order.Property(item => item.Status).HasMaxLength(32);
            order.Property(item => item.Notes).HasMaxLength(2000);
            order.Property(item => item.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            order.Property(item => item.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            order.HasIndex(item => item.Code).IsUnique();
            order.HasIndex(item => new { item.CustomerId, item.CreatedAt });
            order.HasIndex(item => new { item.Status, item.CreatedAt });
            order.HasOne<Customer>().WithMany().HasForeignKey(item => item.CustomerId).OnDelete(DeleteBehavior.Restrict);
            order.HasOne<User>().WithMany().HasForeignKey(item => item.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            order.ToTable("sales_orders", table =>
            {
                table.HasCheckConstraint("CK_sales_orders_total_nonnegative", "total_amount >= 0");
                table.HasCheckConstraint("CK_sales_orders_status", "status IN ('Draft','Confirmed','PartiallyFulfilled','Fulfilled','Cancelled')");
            });
        });

        modelBuilder.Entity<SalesOrderItem>(line =>
        {
            line.HasIndex(item => new { item.SalesOrderId, item.ProductId }).IsUnique();
            line.HasOne<SalesOrder>().WithMany().HasForeignKey(item => item.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
            line.HasOne<Product>().WithMany().HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Restrict);
            line.ToTable("sales_order_items", table =>
            {
                table.HasCheckConstraint("CK_sales_order_items_quantity", "quantity > 0");
                table.HasCheckConstraint("CK_sales_order_items_fulfilled", "fulfilled_quantity >= 0 AND fulfilled_quantity <= quantity");
                table.HasCheckConstraint("CK_sales_order_items_price", "unit_price >= 0");
            });
        });

        modelBuilder.Entity<SalesOrderFulfillment>(fulfillment =>
        {
            fulfillment.Property(item => item.PayloadFingerprint).HasMaxLength(64);
            fulfillment.Property(item => item.Reason).HasMaxLength(1000);
            fulfillment.Property(item => item.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            fulfillment.HasIndex(item => item.RequestId).IsUnique().HasDatabaseName("IX_sales_order_fulfillments_request_id");
            fulfillment.HasIndex(item => new { item.SalesOrderId, item.CreatedAt });
            fulfillment.HasOne<SalesOrder>().WithMany().HasForeignKey(item => item.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
            fulfillment.HasOne<User>().WithMany().HasForeignKey(item => item.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SalesOrderFulfillmentItem>(line =>
        {
            line.HasIndex(item => new { item.FulfillmentId, item.SalesOrderItemId }).IsUnique();
            line.HasIndex(item => item.MovementId).IsUnique();
            line.HasOne<SalesOrderFulfillment>().WithMany().HasForeignKey(item => item.FulfillmentId).OnDelete(DeleteBehavior.Restrict);
            line.HasOne<SalesOrderItem>().WithMany().HasForeignKey(item => item.SalesOrderItemId).OnDelete(DeleteBehavior.Restrict);
            line.HasOne<Product>().WithMany().HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Restrict);
            line.HasOne<StockMovement>().WithMany().HasForeignKey(item => item.MovementId).OnDelete(DeleteBehavior.Restrict);
            line.ToTable("sales_order_fulfillment_items", table =>
                table.HasCheckConstraint("CK_sales_order_fulfillment_items_quantity", "quantity > 0"));
        });
    }
}
