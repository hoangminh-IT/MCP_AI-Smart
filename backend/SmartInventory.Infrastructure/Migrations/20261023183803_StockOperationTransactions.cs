using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StockOperationTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "inventory",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<int>(
                name: "created_by",
                table: "stock_movements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "quantity_after",
                table: "stock_movements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "quantity_before",
                table: "stock_movements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reason",
                table: "stock_movements",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "reference_id",
                table: "stock_movements",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reference_type",
                table: "stock_movements",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "request_id",
                table: "stock_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_created_by",
                table: "stock_movements",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_request_id",
                table: "stock_movements",
                column: "request_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_movements_users_created_by",
                table: "stock_movements",
                column: "created_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stock_movements_users_created_by",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_created_by",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_request_id",
                table: "stock_movements");

            migrationBuilder.DropColumn(name: "updated_at", table: "inventory");
            migrationBuilder.DropColumn(name: "created_by", table: "stock_movements");
            migrationBuilder.DropColumn(name: "quantity_after", table: "stock_movements");
            migrationBuilder.DropColumn(name: "quantity_before", table: "stock_movements");
            migrationBuilder.DropColumn(name: "reason", table: "stock_movements");
            migrationBuilder.DropColumn(name: "reference_id", table: "stock_movements");
            migrationBuilder.DropColumn(name: "reference_type", table: "stock_movements");
            migrationBuilder.DropColumn(name: "request_id", table: "stock_movements");

        }
    }
}
