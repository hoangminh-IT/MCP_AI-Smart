using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Entities;

namespace SmartInventory.Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<McpToolExecutionLog> McpToolExecutionLogs => Set<McpToolExecutionLog>();
    public DbSet<AiChatSession> AiChatSessions => Set<AiChatSession>();
    public DbSet<AiChatMessage> AiChatMessages => Set<AiChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
