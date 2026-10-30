using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Entities;

namespace SmartInventory.Infrastructure.Data;

internal static class SettingsAuditModelConfiguration
{
    internal static void ConfigureSettingsAndAudit(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationSettings>(settings =>
        {
            settings.Property(item => item.Id).ValueGeneratedNever();
            settings.Property(item => item.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            settings.ToTable("application_settings", table =>
            {
                table.HasCheckConstraint("CK_application_settings_singleton", "id = 1");
                table.HasCheckConstraint("CK_application_settings_min_stock", "default_min_stock >= 0");
                table.HasCheckConstraint("CK_application_settings_report_days", "report_default_days BETWEEN 1 AND 366");
                table.HasCheckConstraint("CK_application_settings_revision", "revision > 0");
            });
            // A fixed migration seed avoids model drift on every build.
            settings.HasData(new ApplicationSettings
            {
                Id = 1, CompanyName = "AI Smart Inventory", DefaultMinStock = 0, ReportDefaultDays = 30,
                Revision = 1, UpdatedAt = new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc)
            });
        });
        modelBuilder.Entity<UserPreferences>(preferences =>
        {
            preferences.Property(item => item.UserId).ValueGeneratedNever();
            preferences.Property(item => item.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            preferences.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
            preferences.ToTable("user_preferences", table =>
            {
                table.HasCheckConstraint("CK_user_preferences_page_size", "default_page_size IN (10,25,50)");
                table.HasCheckConstraint("CK_user_preferences_revision", "revision > 0");
            });
        });
        modelBuilder.Entity<AuditLog>(audit =>
        {
            audit.Property(item => item.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            audit.HasOne<User>().WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
            audit.HasIndex(item => new { item.CreatedAt, item.Id });
            audit.HasIndex(item => new { item.ActorId, item.CreatedAt });
            audit.HasIndex(item => new { item.EntityName, item.TargetId, item.CreatedAt });
            audit.HasIndex(item => new { item.Status, item.CreatedAt });
            audit.ToTable("audit_logs", table =>
                table.HasCheckConstraint("CK_audit_logs_status", "status IN ('Succeeded','Failed')"));
        });
    }
}
