using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Entities;

namespace SmartInventory.Infrastructure.Data;

internal static class PurchaseOrderModelConfiguration
{
    internal static void ConfigurePurchaseOrders(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PurchaseOrder>(order =>
        {
            order.Property(item => item.Code).HasMaxLength(40);
            order.Property(item => item.Status).HasMaxLength(32);
            order.Property(item => item.Notes).HasMaxLength(2000);
            order.Property(item => item.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            order.Property(item => item.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            order.HasIndex(item => item.Code).IsUnique();
            order.HasIndex(item => new { item.SupplierId, item.CreatedAt });
            order.HasIndex(item => new { item.Status, item.CreatedAt });
            order.HasOne<Supplier>().WithMany().HasForeignKey(item => item.SupplierId).OnDelete(DeleteBehavior.Restrict);
            order.HasOne<User>().WithMany().HasForeignKey(item => item.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            order.ToTable("purchase_orders", table =>
            {
                table.HasCheckConstraint("CK_purchase_orders_total_nonnegative", "total_amount >= 0");
                table.HasCheckConstraint("CK_purchase_orders_status", "status IN ('Draft','Submitted','Approved','PartiallyReceived','Received','Cancelled')");
            });
        });

        modelBuilder.Entity<PurchaseOrderItem>(line =>
        {
            line.HasIndex(item => new { item.PurchaseOrderId, item.ProductId }).IsUnique();
            line.HasOne<PurchaseOrder>().WithMany().HasForeignKey(item => item.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
            line.HasOne<Product>().WithMany().HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Restrict);
            line.ToTable("purchase_order_items", table =>
            {
                table.HasCheckConstraint("CK_purchase_order_items_quantity", "quantity > 0");
                table.HasCheckConstraint("CK_purchase_order_items_received", "received_quantity >= 0 AND received_quantity <= quantity");
                table.HasCheckConstraint("CK_purchase_order_items_cost", "unit_cost >= 0");
            });
        });

        modelBuilder.Entity<PurchaseOrderReceipt>(receipt =>
        {
            receipt.Property(item => item.PayloadFingerprint).HasMaxLength(64);
            receipt.Property(item => item.Reason).HasMaxLength(1000);
            receipt.Property(item => item.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            receipt.HasIndex(item => item.RequestId).IsUnique().HasDatabaseName("IX_purchase_order_receipts_request_id");
            receipt.HasIndex(item => new { item.PurchaseOrderId, item.CreatedAt });
            receipt.HasOne<PurchaseOrder>().WithMany().HasForeignKey(item => item.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
            receipt.HasOne<User>().WithMany().HasForeignKey(item => item.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PurchaseOrderReceiptItem>(line =>
        {
            line.HasIndex(item => new { item.ReceiptId, item.PurchaseOrderItemId }).IsUnique();
            line.HasIndex(item => item.MovementId).IsUnique();
            line.HasOne<PurchaseOrderReceipt>().WithMany().HasForeignKey(item => item.ReceiptId).OnDelete(DeleteBehavior.Restrict);
            line.HasOne<PurchaseOrderItem>().WithMany().HasForeignKey(item => item.PurchaseOrderItemId).OnDelete(DeleteBehavior.Restrict);
            line.HasOne<Product>().WithMany().HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Restrict);
            line.HasOne<StockMovement>().WithMany().HasForeignKey(item => item.MovementId).OnDelete(DeleteBehavior.Restrict);
            line.ToTable("purchase_order_receipt_items", table =>
                table.HasCheckConstraint("CK_purchase_order_receipt_items_quantity", "quantity > 0"));
        });
    }
}
