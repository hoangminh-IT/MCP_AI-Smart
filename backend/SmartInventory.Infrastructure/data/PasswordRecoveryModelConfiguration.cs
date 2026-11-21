using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Entities;

namespace SmartInventory.Infrastructure.Data;

internal static class PasswordRecoveryModelConfiguration
{
    internal static void ConfigurePasswordRecovery(this ModelBuilder model)
    {
        model.Entity<PasswordResetToken>(token =>
        {
            token.Property(item => item.Id).ValueGeneratedNever();
            token.Property(item => item.TokenHash).HasMaxLength(64);
            token.HasIndex(item => item.TokenHash).IsUnique();
            token.HasIndex(item => new { item.UserId, item.ConsumedAt });
            token.HasIndex(item => item.ExpiresAt);
            token.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
            token.ToTable("password_reset_tokens", table =>
            {
                table.HasCheckConstraint("CK_password_reset_tokens_hash", "token_hash ~ '^[0-9A-F]{64}$'");
                table.HasCheckConstraint("CK_password_reset_tokens_expiry", "expires_at > created_at");
                table.HasCheckConstraint("CK_password_reset_tokens_version", "token_version >= 0");
                table.HasCheckConstraint("CK_password_reset_tokens_consumed", "consumed_at IS NULL OR consumed_at >= created_at");
            });
        });
    }
}
