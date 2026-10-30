using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class McpWriteConfirmations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mcp_write_intents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    tool_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    arguments = table.Column<string>(type: "jsonb", nullable: false),
                    arguments_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    proof_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    result = table.Column<string>(type: "jsonb", nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mcp_write_intents", x => x.id);
                    table.CheckConstraint("CK_mcp_write_intents_approval", "status NOT IN ('Approved','Succeeded') OR proof_hash IS NOT NULL");
                    table.CheckConstraint("CK_mcp_write_intents_expiry", "expires_at > created_at");
                    table.CheckConstraint("CK_mcp_write_intents_result", "status <> 'Succeeded' OR result IS NOT NULL");
                    table.CheckConstraint("CK_mcp_write_intents_status", "status IN ('Pending','Approved','Succeeded','Cancelled')");
                    table.ForeignKey(
                        name: "FK_mcp_write_intents_ai_chat_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "ai_chat_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_mcp_write_intents_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mcp_write_intents_proof_hash",
                table: "mcp_write_intents",
                column: "proof_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mcp_write_intents_session_id_created_at",
                table: "mcp_write_intents",
                columns: new[] { "session_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_mcp_write_intents_user_id_created_at",
                table: "mcp_write_intents",
                columns: new[] { "user_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mcp_write_intents");
        }
    }
}
