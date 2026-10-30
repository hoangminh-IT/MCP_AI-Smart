using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartInventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseOrderManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "purchase_orders",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    supplier_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    created_by = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_orders", x => x.id);
                    table.CheckConstraint("CK_purchase_orders_status", "status IN ('Draft','Submitted','Approved','PartiallyReceived','Received','Cancelled')");
                    table.CheckConstraint("CK_purchase_orders_total_nonnegative", "total_amount >= 0");
                    table.ForeignKey(
                        name: "FK_purchase_orders_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_orders_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    purchase_order_id = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    received_quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_items", x => x.id);
                    table.CheckConstraint("CK_purchase_order_items_cost", "unit_cost >= 0");
                    table.CheckConstraint("CK_purchase_order_items_quantity", "quantity > 0");
                    table.CheckConstraint("CK_purchase_order_items_received", "received_quantity >= 0 AND received_quantity <= quantity");
                    table.ForeignKey(
                        name: "FK_purchase_order_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_items_purchase_orders_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<int>(type: "integer", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payload_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    response_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    created_by = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_receipts", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_order_receipts_purchase_orders_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_receipts_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_receipt_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_item_id = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    movement_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_receipt_items", x => x.id);
                    table.CheckConstraint("CK_purchase_order_receipt_items_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "FK_purchase_order_receipt_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_receipt_items_purchase_order_items_purchase_~",
                        column: x => x.purchase_order_item_id,
                        principalTable: "purchase_order_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_receipt_items_purchase_order_receipts_receip~",
                        column: x => x.receipt_id,
                        principalTable: "purchase_order_receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_receipt_items_stock_movements_movement_id",
                        column: x => x.movement_id,
                        principalTable: "stock_movements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_items_product_id",
                table: "purchase_order_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_items_purchase_order_id_product_id",
                table: "purchase_order_items",
                columns: new[] { "purchase_order_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_receipt_items_movement_id",
                table: "purchase_order_receipt_items",
                column: "movement_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_receipt_items_product_id",
                table: "purchase_order_receipt_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_receipt_items_purchase_order_item_id",
                table: "purchase_order_receipt_items",
                column: "purchase_order_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_receipt_items_receipt_id_purchase_order_item~",
                table: "purchase_order_receipt_items",
                columns: new[] { "receipt_id", "purchase_order_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_receipts_created_by",
                table: "purchase_order_receipts",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_receipts_purchase_order_id_created_at",
                table: "purchase_order_receipts",
                columns: new[] { "purchase_order_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_receipts_request_id",
                table: "purchase_order_receipts",
                column: "request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_code",
                table: "purchase_orders",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_created_by",
                table: "purchase_orders",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_status_created_at",
                table: "purchase_orders",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_supplier_id_created_at",
                table: "purchase_orders",
                columns: new[] { "supplier_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_order_receipt_items");

            migrationBuilder.DropTable(
                name: "purchase_order_items");

            migrationBuilder.DropTable(
                name: "purchase_order_receipts");

            migrationBuilder.DropTable(
                name: "purchase_orders");
        }
    }
}
