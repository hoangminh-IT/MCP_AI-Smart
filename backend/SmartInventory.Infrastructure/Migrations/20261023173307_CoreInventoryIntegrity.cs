using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartInventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CoreInventoryIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "product_id",
                table: "inventory",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_product_id_created_at",
                table: "stock_movements",
                columns: new[] { "product_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_products_category_id",
                table: "products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_name",
                table: "products",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_products_sku",
                table: "products",
                column: "sku",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_products_min_stock_nonnegative",
                table: "products",
                sql: "min_stock >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_products_price_nonnegative",
                table: "products",
                sql: "price >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_quantity_nonnegative",
                table: "inventory",
                sql: "quantity >= 0");

            migrationBuilder.AddForeignKey(
                name: "FK_inventory_products_product_id",
                table: "inventory",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_categories_category_id",
                table: "products",
                column: "category_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_movements_products_product_id",
                table: "stock_movements",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_inventory_products_product_id",
                table: "inventory");

            migrationBuilder.DropForeignKey(
                name: "FK_products_categories_category_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_movements_products_product_id",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "IX_users_username",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_product_id_created_at",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "IX_products_category_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_name",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_sku",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_products_min_stock_nonnegative",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_products_price_nonnegative",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_quantity_nonnegative",
                table: "inventory");

            migrationBuilder.AlterColumn<int>(
                name: "product_id",
                table: "inventory",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
        }
    }
}
