using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartInventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SalesOrderManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sales_orders",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    customer_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    created_by = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_orders", x => x.id);
                    table.CheckConstraint("CK_sales_orders_status", "status IN ('Draft','Confirmed','PartiallyFulfilled','Fulfilled','Cancelled')");
                    table.CheckConstraint("CK_sales_orders_total_nonnegative", "total_amount >= 0");
                    table.ForeignKey(
                        name: "FK_sales_orders_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_orders_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_order_fulfillments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_order_id = table.Column<int>(type: "integer", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payload_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    response_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    created_by = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_order_fulfillments", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_order_fulfillments_sales_orders_sales_order_id",
                        column: x => x.sales_order_id,
                        principalTable: "sales_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_order_fulfillments_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_order_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sales_order_id = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    fulfilled_quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_order_items", x => x.id);
                    table.CheckConstraint("CK_sales_order_items_fulfilled", "fulfilled_quantity >= 0 AND fulfilled_quantity <= quantity");
                    table.CheckConstraint("CK_sales_order_items_price", "unit_price >= 0");
                    table.CheckConstraint("CK_sales_order_items_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "FK_sales_order_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_order_items_sales_orders_sales_order_id",
                        column: x => x.sales_order_id,
                        principalTable: "sales_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_order_fulfillment_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fulfillment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_order_item_id = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    movement_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_order_fulfillment_items", x => x.id);
                    table.CheckConstraint("CK_sales_order_fulfillment_items_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "FK_sales_order_fulfillment_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_order_fulfillment_items_sales_order_fulfillments_fulf~",
                        column: x => x.fulfillment_id,
                        principalTable: "sales_order_fulfillments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_order_fulfillment_items_sales_order_items_sales_order~",
                        column: x => x.sales_order_item_id,
                        principalTable: "sales_order_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_order_fulfillment_items_stock_movements_movement_id",
                        column: x => x.movement_id,
                        principalTable: "stock_movements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_fulfillment_items_fulfillment_id_sales_order_it~",
                table: "sales_order_fulfillment_items",
                columns: new[] { "fulfillment_id", "sales_order_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_fulfillment_items_movement_id",
                table: "sales_order_fulfillment_items",
                column: "movement_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_fulfillment_items_product_id",
                table: "sales_order_fulfillment_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_fulfillment_items_sales_order_item_id",
                table: "sales_order_fulfillment_items",
                column: "sales_order_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_fulfillments_created_by",
                table: "sales_order_fulfillments",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_fulfillments_request_id",
                table: "sales_order_fulfillments",
                column: "request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_fulfillments_sales_order_id_created_at",
                table: "sales_order_fulfillments",
                columns: new[] { "sales_order_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_items_product_id",
                table: "sales_order_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_items_sales_order_id_product_id",
                table: "sales_order_items",
                columns: new[] { "sales_order_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_orders_code",
                table: "sales_orders",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_orders_created_by",
                table: "sales_orders",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_sales_orders_customer_id_created_at",
                table: "sales_orders",
                columns: new[] { "customer_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_orders_status_created_at",
                table: "sales_orders",
                columns: new[] { "status", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sales_order_fulfillment_items");

            migrationBuilder.DropTable(
                name: "sales_order_fulfillments");

            migrationBuilder.DropTable(
                name: "sales_order_items");

            migrationBuilder.DropTable(
                name: "sales_orders");
        }
    }
}
