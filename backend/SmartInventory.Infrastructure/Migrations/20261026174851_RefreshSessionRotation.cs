using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefreshSessionRotation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "refresh_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    token_version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    replaced_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_sessions", x => x.id);
                    table.CheckConstraint("CK_refresh_sessions_expiry", "expires_at > created_at");
                    table.CheckConstraint("CK_refresh_sessions_hash", "token_hash ~ '^[0-9A-F]{64}$'");
                    table.CheckConstraint("CK_refresh_sessions_revocation", "revoked_at IS NULL OR revoked_at >= created_at");
                    table.CheckConstraint("CK_refresh_sessions_rotation", "(consumed_at IS NULL AND replaced_by_id IS NULL) OR (consumed_at IS NOT NULL AND consumed_at >= created_at AND replaced_by_id IS NOT NULL AND replaced_by_id <> id)");
                    table.CheckConstraint("CK_refresh_sessions_version", "token_version >= 0");
                    table.ForeignKey(
                        name: "FK_refresh_sessions_refresh_sessions_replaced_by_id",
                        column: x => x.replaced_by_id,
                        principalTable: "refresh_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refresh_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_sessions_expires_at",
                table: "refresh_sessions",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_sessions_family_id",
                table: "refresh_sessions",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_sessions_replaced_by_id",
                table: "refresh_sessions",
                column: "replaced_by_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_sessions_token_hash",
                table: "refresh_sessions",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_sessions_user_id_revoked_at",
                table: "refresh_sessions",
                columns: new[] { "user_id", "revoked_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refresh_sessions");
        }
    }
}
