using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Entities;

namespace SmartInventory.Infrastructure.Data;

internal static class RefreshSessionModelConfiguration
{
    internal static void ConfigureRefreshSessions(this ModelBuilder model)
    {
        model.Entity<RefreshSession>(session =>
        {
            session.Property(item => item.Id).ValueGeneratedNever();
            session.HasIndex(item => item.TokenHash).IsUnique();
            session.HasIndex(item => new { item.UserId, item.RevokedAt });
            session.HasIndex(item => item.ExpiresAt);
            session.HasIndex(item => item.FamilyId);
            session.HasIndex(item => item.ReplacedById).IsUnique();
            session.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
            session.HasOne<RefreshSession>().WithMany().HasForeignKey(item => item.ReplacedById).OnDelete(DeleteBehavior.Restrict);
            session.ToTable("refresh_sessions", table =>
            {
                table.HasCheckConstraint("CK_refresh_sessions_hash", "token_hash ~ '^[0-9A-F]{64}$'");
                table.HasCheckConstraint("CK_refresh_sessions_expiry", "expires_at > created_at");
                table.HasCheckConstraint("CK_refresh_sessions_version", "token_version >= 0");
                table.HasCheckConstraint("CK_refresh_sessions_rotation",
                    "(consumed_at IS NULL AND replaced_by_id IS NULL) OR (consumed_at IS NOT NULL AND consumed_at >= created_at AND replaced_by_id IS NOT NULL AND replaced_by_id <> id)");
                table.HasCheckConstraint("CK_refresh_sessions_revocation", "revoked_at IS NULL OR revoked_at >= created_at");
            });
        });
    }
}
