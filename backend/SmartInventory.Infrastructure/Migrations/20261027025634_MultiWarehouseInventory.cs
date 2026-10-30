using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using SmartInventory.Infrastructure.Data;

#nullable disable

namespace SmartInventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MultiWarehouseInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "warehouse_id",
                table: "stock_movements",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "warehouse_quantity_after",
                table: "stock_movements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "warehouse_quantity_before",
                table: "stock_movements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "warehouse_id",
                table: "sales_orders",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "warehouse_id",
                table: "purchase_orders",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "warehouses",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouses", x => x.id);
                    table.CheckConstraint("CK_warehouses_default", "(id = 1 AND is_default AND code = 'MAIN' AND is_active) OR (id <> 1 AND NOT is_default)");
                });

            migrationBuilder.CreateTable(
                name: "stock_transfers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    source_warehouse_id = table.Column<int>(type: "integer", nullable: false),
                    destination_warehouse_id = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    payload_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    created_by = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_transfers", x => x.id);
                    table.CheckConstraint("CK_stock_transfers_fingerprint", "payload_fingerprint ~ '^[0-9A-F]{64}$'");
                    table.CheckConstraint("CK_stock_transfers_quantity", "quantity > 0");
                    table.CheckConstraint("CK_stock_transfers_warehouses", "source_warehouse_id <> destination_warehouse_id");
                    table.ForeignKey(
                        name: "FK_stock_transfers_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_transfers_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_transfers_warehouses_destination_warehouse_id",
                        column: x => x.destination_warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_transfers_warehouses_source_warehouse_id",
                        column: x => x.source_warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warehouse_inventory",
                columns: table => new
                {
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    warehouse_id = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse_inventory", x => new { x.product_id, x.warehouse_id });
                    table.CheckConstraint("CK_warehouse_inventory_quantity", "quantity >= 0");
                    table.ForeignKey(
                        name: "FK_warehouse_inventory_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warehouse_inventory_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Backfill existing balances verbatim before attaching historical MAIN foreign keys.
            // Do not create movements or infer unknown historical local before/after values.
            migrationBuilder.Sql(WarehouseIntegritySql.SeedMain);

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_warehouse_id_created_at",
                table: "stock_movements",
                columns: new[] { "warehouse_id", "created_at" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_stock_movements_warehouse_after",
                table: "stock_movements",
                sql: "warehouse_quantity_after IS NULL OR warehouse_quantity_after >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_stock_movements_warehouse_before",
                table: "stock_movements",
                sql: "warehouse_quantity_before IS NULL OR warehouse_quantity_before >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_sales_orders_warehouse_id_status",
                table: "sales_orders",
                columns: new[] { "warehouse_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_warehouse_id_status",
                table: "purchase_orders",
                columns: new[] { "warehouse_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_created_by",
                table: "stock_transfers",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_destination_warehouse_id_created_at",
                table: "stock_transfers",
                columns: new[] { "destination_warehouse_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_product_id",
                table: "stock_transfers",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_request_id",
                table: "stock_transfers",
                column: "request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_source_warehouse_id_created_at",
                table: "stock_transfers",
                columns: new[] { "source_warehouse_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_inventory_warehouse_id_product_id",
                table: "warehouse_inventory",
                columns: new[] { "warehouse_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_code",
                table: "warehouses",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_is_active_name",
                table: "warehouses",
                columns: new[] { "is_active", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_is_default",
                table: "warehouses",
                column: "is_default",
                unique: true,
                filter: "is_default");

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_orders_warehouses_warehouse_id",
                table: "purchase_orders",
                column: "warehouse_id",
                principalTable: "warehouses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sales_orders_warehouses_warehouse_id",
                table: "sales_orders",
                column: "warehouse_id",
                principalTable: "warehouses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_movements_warehouses_warehouse_id",
                table: "stock_movements",
                column: "warehouse_id",
                principalTable: "warehouses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(WarehouseIntegritySql.CreateConstraints);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(WarehouseIntegritySql.DropConstraints);
            migrationBuilder.DropForeignKey(
                name: "FK_purchase_orders_warehouses_warehouse_id",
                table: "purchase_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_sales_orders_warehouses_warehouse_id",
                table: "sales_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_movements_warehouses_warehouse_id",
                table: "stock_movements");

            migrationBuilder.DropTable(
                name: "stock_transfers");

            migrationBuilder.DropTable(
                name: "warehouse_inventory");

            migrationBuilder.DropTable(
                name: "warehouses");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_warehouse_id_created_at",
                table: "stock_movements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_stock_movements_warehouse_after",
                table: "stock_movements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_stock_movements_warehouse_before",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "IX_sales_orders_warehouse_id_status",
                table: "sales_orders");

            migrationBuilder.DropIndex(
                name: "IX_purchase_orders_warehouse_id_status",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "warehouse_id",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "warehouse_quantity_after",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "warehouse_quantity_before",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "warehouse_id",
                table: "sales_orders");

            migrationBuilder.DropColumn(
                name: "warehouse_id",
                table: "purchase_orders");
        }
    }
}
