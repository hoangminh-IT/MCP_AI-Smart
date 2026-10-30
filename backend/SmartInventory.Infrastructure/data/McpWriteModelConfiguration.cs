using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Entities;

namespace SmartInventory.Infrastructure.Data;

internal static class McpWriteModelConfiguration
{
    internal static void ConfigureMcpWrites(this ModelBuilder builder)
    {
        builder.Entity<McpWriteIntent>(intent =>
        {
            intent.Property(item => item.Id).ValueGeneratedNever();
            intent.Property(item => item.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            intent.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
            intent.HasOne<AiChatSession>().WithMany().HasForeignKey(item => item.SessionId).OnDelete(DeleteBehavior.SetNull);
            intent.HasIndex(item => new { item.UserId, item.CreatedAt });
            intent.HasIndex(item => new { item.SessionId, item.CreatedAt });
            intent.HasIndex(item => item.ProofHash).IsUnique();
            intent.ToTable("mcp_write_intents", table =>
            {
                table.HasCheckConstraint("CK_mcp_write_intents_status", "status IN ('Pending','Approved','Succeeded','Cancelled')");
                table.HasCheckConstraint("CK_mcp_write_intents_expiry", "expires_at > created_at");
                table.HasCheckConstraint("CK_mcp_write_intents_result", "status <> 'Succeeded' OR result IS NOT NULL");
                table.HasCheckConstraint("CK_mcp_write_intents_approval", "status NOT IN ('Approved','Succeeded') OR proof_hash IS NOT NULL");
            });
        });
    }
}
