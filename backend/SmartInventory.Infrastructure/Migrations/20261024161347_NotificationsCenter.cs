using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NotificationsCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    deduplication_key = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    target_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    target_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_notifications", x => x.id);
                    table.CheckConstraint("CK_user_notifications_target", "(target_type IS NULL AND target_id IS NULL) OR (target_type IS NOT NULL AND target_id IS NOT NULL)");
                    table.CheckConstraint("CK_user_notifications_type", "type IN ('LowStock','OutOfStock','PurchaseOrder','SalesOrder')");
                    table.ForeignKey(
                        name: "FK_user_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_notifications_user_id_deduplication_key",
                table: "user_notifications",
                columns: new[] { "user_id", "deduplication_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_notifications_user_id_read_at_created_at",
                table: "user_notifications",
                columns: new[] { "user_id", "read_at", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_notifications");
        }
    }
}
