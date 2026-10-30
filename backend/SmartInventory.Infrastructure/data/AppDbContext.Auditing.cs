using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SmartInventory.Application.Entities;

namespace SmartInventory.Infrastructure.Data;

public sealed partial class AppDbContext
{
    private static readonly HashSet<Type> AuditedTypes =
    [
        typeof(User), typeof(Category), typeof(Product), typeof(Supplier), typeof(Customer),
        typeof(Inventory), typeof(StockMovement), typeof(PurchaseOrder), typeof(PurchaseOrderItem),
        typeof(PurchaseOrderReceipt), typeof(PurchaseOrderReceiptItem), typeof(SalesOrder), typeof(SalesOrderItem),
        typeof(SalesOrderFulfillment), typeof(SalesOrderFulfillmentItem), typeof(ApplicationSettings), typeof(UserPreferences),
        typeof(McpWriteIntent), typeof(RefreshSession), typeof(Warehouse), typeof(WarehouseInventory), typeof(StockTransfer)
    ];

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        var entries = PendingAuditEntries();
        if (entries.Count == 0) return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        await using var transaction = Database.CurrentTransaction is null
            ? await Database.BeginTransactionAsync(cancellationToken) : null;
        // Do not accept tracked changes until both the business SQL and audit
        // SQL succeed. Raw audit insertion preserves SaveChanges(false) semantics
        // without running a second EF save over still-pending business entries.
        var affected = await base.SaveChangesAsync(false, cancellationToken);
        await Database.ExecuteSqlInterpolatedAsync(AuditInsert(entries), cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        if (acceptAllChangesOnSuccess) ChangeTracker.AcceptAllChanges();
        return affected;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var entries = PendingAuditEntries();
        if (entries.Count == 0) return base.SaveChanges(acceptAllChangesOnSuccess);
        using var transaction = Database.CurrentTransaction is null ? Database.BeginTransaction() : null;
        var affected = base.SaveChanges(false);
        Database.ExecuteSqlInterpolated(AuditInsert(entries));
        transaction?.Commit();
        if (acceptAllChangesOnSuccess) ChangeTracker.AcceptAllChanges();
        return affected;
    }

    private List<(EntityEntry Entry, string Action)> PendingAuditEntries()
    {
        if (ChangeTracker.AutoDetectChangesEnabled) ChangeTracker.DetectChanges();
        var entries = ChangeTracker.Entries().ToArray();
        if (entries.Any(entry => entry.Entity is AuditLog && entry.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Audit records are append-only.");
        return entries.Where(entry => AuditedTypes.Contains(entry.Metadata.ClrType) &&
                entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => (entry, entry.State switch
            {
                EntityState.Added => "Created", EntityState.Modified => "Updated", _ => "Deleted"
            })).ToList();
    }

    private FormattableString AuditInsert(IReadOnlyList<(EntityEntry Entry, string Action)> entries)
    {
        var now = DateTime.UtcNow;
        var payload = JsonSerializer.Serialize(entries.Select(item => new
        {
            actor_id = actor?.UserId,
            action = item.Action,
            entity_name = item.Entry.Metadata.ClrType.Name,
            target_id = TargetKey(item.Entry),
            status = "Succeeded",
            created_at = now
        }));
        // The payload contains only fixed metadata and generated keys. No entity
        // property values, passwords, tokens or request bodies are inspected.
        return $"""
            INSERT INTO audit_logs (actor_id, action, entity_name, target_id, status, created_at)
            SELECT row.actor_id, row.action, row.entity_name, row.target_id, row.status, row.created_at
            FROM jsonb_to_recordset(CAST({payload} AS jsonb)) AS row(
                actor_id integer, action text, entity_name text, target_id text, status text, created_at timestamptz)
            """;
    }

    private static string? TargetKey(EntityEntry entry)
    {
        var properties = entry.Metadata.FindPrimaryKey()?.Properties;
        return properties is null ? null : string.Join("/", properties.Select(property =>
            Convert.ToString(entry.Property(property.Name).CurrentValue, CultureInfo.InvariantCulture)));
    }
}
