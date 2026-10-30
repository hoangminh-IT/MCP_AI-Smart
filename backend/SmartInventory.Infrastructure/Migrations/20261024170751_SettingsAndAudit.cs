using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartInventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SettingsAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "application_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    company_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contact_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    address = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    default_min_stock = table.Column<int>(type: "integer", nullable: false),
                    report_default_days = table.Column<int>(type: "integer", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_settings", x => x.id);
                    table.CheckConstraint("CK_application_settings_min_stock", "default_min_stock >= 0");
                    table.CheckConstraint("CK_application_settings_report_days", "report_default_days BETWEEN 1 AND 366");
                    table.CheckConstraint("CK_application_settings_revision", "revision > 0");
                    table.CheckConstraint("CK_application_settings_singleton", "id = 1");
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    actor_id = table.Column<int>(type: "integer", nullable: true),
                    action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    entity_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    target_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                    table.CheckConstraint("CK_audit_logs_status", "status IN ('Succeeded','Failed')");
                    table.ForeignKey(
                        name: "FK_audit_logs_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_preferences",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    default_page_size = table.Column<int>(type: "integer", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_preferences", x => x.user_id);
                    table.CheckConstraint("CK_user_preferences_page_size", "default_page_size IN (10,25,50)");
                    table.CheckConstraint("CK_user_preferences_revision", "revision > 0");
                    table.ForeignKey(
                        name: "FK_user_preferences_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "application_settings",
                columns: new[] { "id", "address", "company_name", "contact_email", "default_min_stock", "phone", "report_default_days", "revision", "updated_at" },
                values: new object[] { 1, null, "AI Smart Inventory", null, 0, null, 30, 1, new DateTime(2026, 10, 24, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_actor_id_created_at",
                table: "audit_logs",
                columns: new[] { "actor_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_created_at_id",
                table: "audit_logs",
                columns: new[] { "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_entity_name_target_id_created_at",
                table: "audit_logs",
                columns: new[] { "entity_name", "target_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_status_created_at",
                table: "audit_logs",
                columns: new[] { "status", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_settings");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "user_preferences");
        }
    }
}
