using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Common;
using SmartInventory.Application.Entities;

namespace SmartInventory.Infrastructure.Data;

public sealed partial class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentActor? currentActor = null)
    : DbContext(options)
{
    private readonly ICurrentActor? actor = currentActor;

    public DbSet<ApplicationSettings> ApplicationSettings => Set<ApplicationSettings>();
    public DbSet<UserPreferences> UserPreferences => Set<UserPreferences>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<PurchaseOrderReceipt> PurchaseOrderReceipts => Set<PurchaseOrderReceipt>();
    public DbSet<PurchaseOrderReceiptItem> PurchaseOrderReceiptItems => Set<PurchaseOrderReceiptItem>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderItem> SalesOrderItems => Set<SalesOrderItem>();
    public DbSet<SalesOrderFulfillment> SalesOrderFulfillments => Set<SalesOrderFulfillment>();
    public DbSet<SalesOrderFulfillmentItem> SalesOrderFulfillmentItems => Set<SalesOrderFulfillmentItem>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<WarehouseInventory> WarehouseInventories => Set<WarehouseInventory>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<McpToolExecutionLog> McpToolExecutionLogs => Set<McpToolExecutionLog>();
    public DbSet<McpWriteIntent> McpWriteIntents => Set<McpWriteIntent>();
    public DbSet<AiChatSession> AiChatSessions => Set<AiChatSession>();
    public DbSet<AiChatMessage> AiChatMessages => Set<AiChatMessage>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureSettingsAndAudit();
        modelBuilder.ConfigurePurchaseOrders();
        modelBuilder.ConfigureSalesOrders();
        modelBuilder.ConfigureMcpWrites();
        modelBuilder.ConfigureRefreshSessions();
        modelBuilder.ConfigureWarehouses();

        modelBuilder.Entity<UserNotification>(notification =>
        {
            notification.Property(item => item.DeduplicationKey).HasMaxLength(160);
            notification.Property(item => item.Type).HasMaxLength(40);
            notification.Property(item => item.Title).HasMaxLength(160);
            notification.Property(item => item.Message).HasMaxLength(1000);
            notification.Property(item => item.TargetType).HasMaxLength(40);
            notification.Property(item => item.TargetId).HasMaxLength(100);
            notification.Property(item => item.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            notification.HasIndex(item => new { item.UserId, item.DeduplicationKey }).IsUnique();
            notification.HasIndex(item => new { item.UserId, item.ReadAt, item.CreatedAt });
            notification.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
            notification.ToTable("user_notifications", table =>
            {
                table.HasCheckConstraint("CK_user_notifications_type", "type IN ('LowStock','OutOfStock','PurchaseOrder','SalesOrder')");
                table.HasCheckConstraint("CK_user_notifications_target", "(target_type IS NULL AND target_id IS NULL) OR (target_type IS NOT NULL AND target_id IS NOT NULL)");
            });
        });

        modelBuilder.Entity<User>(user =>
        {
            user.HasIndex(item => item.Username).IsUnique();
            user.Property(item => item.Email).HasDefaultValue(string.Empty);
            user.Property(item => item.FullName).HasDefaultValue(string.Empty);
            user.Property(item => item.IsActive).HasDefaultValue(true);
            user.Property(item => item.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            user.Property(item => item.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            user.Property(item => item.TokenVersion).HasDefaultValue(0);
        });

        modelBuilder.Entity<Category>(category =>
        {
            category.Property(item => item.Description).HasDefaultValue(string.Empty);
            category.Property(item => item.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Supplier>(supplier =>
        {
            supplier.HasIndex(item => item.Code).IsUnique();
            supplier.HasIndex(item => item.Name);
            supplier.HasIndex(item => item.IsActive);
            supplier.Property(item => item.IsActive).HasDefaultValue(true);
            supplier.Property(item => item.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            supplier.Property(item => item.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Customer>(customer =>
        {
            customer.HasIndex(item => item.Code).IsUnique();
            customer.HasIndex(item => item.Name);
            customer.HasIndex(item => item.IsActive);
            customer.Property(item => item.IsActive).HasDefaultValue(true);
            customer.Property(item => item.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            customer.Property(item => item.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Product>(product =>
        {
            product.ToTable("products", table =>
            {
                table.HasCheckConstraint("CK_products_price_nonnegative", "price >= 0");
                table.HasCheckConstraint("CK_products_min_stock_nonnegative", "min_stock >= 0");
                table.HasCheckConstraint("CK_products_cost_price_nonnegative", "cost_price >= 0");
            });

            product.Property(item => item.Description).HasDefaultValue(string.Empty);
            product.Property(item => item.CostPrice).HasDefaultValue(0m);
            product.Property(item => item.IsActive).HasDefaultValue(true);
            product.Property(item => item.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            product.Property(item => item.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            product.HasIndex(item => item.Sku).IsUnique();
            product.HasIndex(item => item.Name);
            product.HasIndex(item => item.CategoryId);
            product.HasOne<Category>()
                .WithMany()
                .HasForeignKey(item => item.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Inventory>(inventory =>
        {
            inventory.ToTable("inventory", table =>
                table.HasCheckConstraint("CK_inventory_quantity_nonnegative", "quantity >= 0"));

            inventory.HasKey(item => item.ProductId);
            inventory.Property(item => item.ProductId).ValueGeneratedNever();
            inventory.Property(item => item.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            inventory.HasOne<Product>()
                .WithOne()
                .HasForeignKey<Inventory>(item => item.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockMovement>(movement =>
        {
            movement.HasIndex(item => new { item.ProductId, item.CreatedAt });
            movement.HasIndex(item => item.RequestId).IsUnique().HasDatabaseName("IX_stock_movements_request_id");
            movement.Property(item => item.Reason).HasMaxLength(1000).HasDefaultValue(string.Empty);
            movement.Property(item => item.ReferenceType).HasMaxLength(80);
            movement.Property(item => item.ReferenceId).HasMaxLength(100);
            movement.HasOne<User>().WithMany().HasForeignKey(item => item.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            movement.ToTable("stock_movements", table =>
            {
                table.HasCheckConstraint("CK_stock_movements_before_nonnegative", "quantity_before IS NULL OR quantity_before >= 0");
                table.HasCheckConstraint("CK_stock_movements_after_nonnegative", "quantity_after IS NULL OR quantity_after >= 0");
            });
            movement.HasOne<Product>()
                .WithMany()
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<McpToolExecutionLog>(execution =>
        {
            execution.HasIndex(item => new { item.UserId, item.CreatedAt });
            execution.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
            execution.Property(item => item.ToolName).HasMaxLength(128);
            execution.Property(item => item.Status).HasMaxLength(32);
            execution.Property(item => item.ErrorMessage).HasMaxLength(1000);
            execution.Property(item => item.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            execution.ToTable("mcp_tool_execution_logs", table =>
                table.HasCheckConstraint("CK_mcp_execution_duration_nonnegative", "duration_ms >= 0"));
        });

        modelBuilder.Entity<AiChatSession>(session =>
        {
            session.HasIndex(item => new { item.UserId, item.UpdatedAt });
            session.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
            session.Property(item => item.Title).HasMaxLength(120);
            session.Property(item => item.Revision).IsConcurrencyToken();
            session.Property(item => item.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            session.Property(item => item.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<AiChatMessage>(message =>
        {
            message.HasIndex(item => new { item.SessionId, item.CreatedAt });
            message.HasOne<AiChatSession>().WithMany().HasForeignKey(item => item.SessionId).OnDelete(DeleteBehavior.Cascade);
            message.Property(item => item.Role).HasMaxLength(16);
            message.Property(item => item.Status).HasMaxLength(32);
            message.Property(item => item.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }
}
