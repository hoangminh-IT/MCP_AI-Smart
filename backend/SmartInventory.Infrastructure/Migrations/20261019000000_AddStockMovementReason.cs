using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartInventory.Infrastructure.Data;

#nullable disable

namespace SmartInventory.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261019000000_AddStockMovementReason")]
public partial class AddStockMovementReason : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(
            name: "reason",
            table: "stock_movements",
            type: "text",
            nullable: false,
            defaultValue: "");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "reason", table: "stock_movements");
}
