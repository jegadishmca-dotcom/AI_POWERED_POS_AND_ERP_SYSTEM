using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PosErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase4InventoryIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "running_points",
                table: "loyalty_ledger",
                newName: "previous_balance");

            migrationBuilder.RenameColumn(
                name: "points",
                table: "loyalty_ledger",
                newName: "points_redeemed");

            migrationBuilder.AddColumn<decimal>(
                name: "balance_after_transaction",
                table: "loyalty_ledger",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "invoice_id",
                table: "loyalty_ledger",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "points_earned",
                table: "loyalty_ledger",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "remarks",
                table: "loyalty_ledger",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "customers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "average_basket_value",
                table: "customers",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "customer_segment",
                table: "customers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "customers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "enrollment_date",
                table: "customers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "last_points_earned_date",
                table: "customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_purchase_date",
                table: "customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_redemption_date",
                table: "customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "lifetime_points_earned",
                table: "customers",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "lifetime_spend",
                table: "customers",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "membership_status",
                table: "customers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "preferred_category",
                table: "customers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "visit_frequency",
                table: "customers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "benefits_json",
                table: "customer_tiers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "minimum_points",
                table: "customer_tiers",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "tier_downgrade_rule",
                table: "customer_tiers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "tier_upgrade_rule",
                table: "customer_tiers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "inventory_aging_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventory_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_date = table.Column<DateTime>(type: "date", nullable: false),
                    age0_to30 = table.Column<decimal>(type: "numeric", nullable: false),
                    age31_to60 = table.Column<decimal>(type: "numeric", nullable: false),
                    age61_to90 = table.Column<decimal>(type: "numeric", nullable: false),
                    age91_to180 = table.Column<decimal>(type: "numeric", nullable: false),
                    age180_plus = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_aging_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventory_aging_snapshots_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inventory_audit_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventory_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    before_quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    after_quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_audit_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_forecasts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventory_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    forecast_date = table.Column<DateTime>(type: "date", nullable: false),
                    forecast_model_version = table.Column<string>(type: "text", nullable: false),
                    historical_demand = table.Column<decimal>(type: "numeric", nullable: false),
                    predicted_demand = table.Column<decimal>(type: "numeric", nullable: false),
                    confidence_percentage = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_forecasts", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventory_forecasts_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inventory_locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    location_type = table.Column<string>(type: "text", nullable: false),
                    parent_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_locations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "loyalty_program_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active_config = table.Column<bool>(type: "boolean", nullable: false),
                    earn_ratio_spend_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    earn_ratio_points = table.Column<decimal>(type: "numeric", nullable: false),
                    redeem_ratio_points = table.Column<decimal>(type: "numeric", nullable: false),
                    redeem_ratio_discount_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    max_redemption_percentage_per_invoice = table.Column<decimal>(type: "numeric", nullable: false),
                    max_redemption_per_day = table.Column<decimal>(type: "numeric", nullable: false),
                    max_manual_adjustment_per_day = table.Column<decimal>(type: "numeric", nullable: false),
                    max_bonus_allocation_per_customer = table.Column<decimal>(type: "numeric", nullable: false),
                    enable_auto_tier_evaluation = table.Column<bool>(type: "boolean", nullable: false),
                    enable_point_expiry = table.Column<bool>(type: "boolean", nullable: false),
                    expiry_months = table.Column<int>(type: "integer", nullable: false),
                    birthday_bonus_points = table.Column<decimal>(type: "numeric", nullable: false),
                    anniversary_bonus_points = table.Column<decimal>(type: "numeric", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loyalty_program_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_store_inventory_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventory_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    min_stock_level = table.Column<decimal>(type: "numeric", nullable: false),
                    max_stock_level = table.Column<decimal>(type: "numeric", nullable: false),
                    reorder_point = table.Column<decimal>(type: "numeric", nullable: false),
                    safety_stock = table.Column<decimal>(type: "numeric", nullable: false),
                    lead_time_days = table.Column<int>(type: "integer", nullable: false),
                    economic_order_quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    reorder_frequency_days = table.Column<int>(type: "integer", nullable: false),
                    preferred_supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    preferred_order_multiple = table.Column<int>(type: "integer", nullable: false),
                    is_auto_reorder_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_store_inventory_policies", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_store_inventory_policies_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_transfer_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    dispatched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_transfer_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "supplier_scorecards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scorecard_date = table.Column<DateTime>(type: "date", nullable: false),
                    on_time_delivery_percentage = table.Column<decimal>(type: "numeric", nullable: false),
                    price_competitiveness_score = table.Column<decimal>(type: "numeric", nullable: false),
                    quality_score = table.Column<decimal>(type: "numeric", nullable: false),
                    rejection_rate = table.Column<decimal>(type: "numeric", nullable: false),
                    overall_rating = table.Column<decimal>(type: "numeric", nullable: false),
                    last_purchase_date = table.Column<DateTime>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_scorecards", x => x.id);
                    table.ForeignKey(
                        name: "FK_supplier_scorecards_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_aging_snapshots_product_id",
                table: "inventory_aging_snapshots",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_forecasts_product_id",
                table: "inventory_forecasts",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_store_inventory_policies_product_id",
                table: "product_store_inventory_policies",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_scorecards_supplier_id",
                table: "supplier_scorecards",
                column: "supplier_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_aging_snapshots");

            migrationBuilder.DropTable(
                name: "inventory_audit_entries");

            migrationBuilder.DropTable(
                name: "inventory_forecasts");

            migrationBuilder.DropTable(
                name: "inventory_locations");

            migrationBuilder.DropTable(
                name: "loyalty_program_configs");

            migrationBuilder.DropTable(
                name: "product_store_inventory_policies");

            migrationBuilder.DropTable(
                name: "stock_transfer_requests");

            migrationBuilder.DropTable(
                name: "supplier_scorecards");

            migrationBuilder.DropColumn(
                name: "balance_after_transaction",
                table: "loyalty_ledger");

            migrationBuilder.DropColumn(
                name: "invoice_id",
                table: "loyalty_ledger");

            migrationBuilder.DropColumn(
                name: "points_earned",
                table: "loyalty_ledger");

            migrationBuilder.DropColumn(
                name: "remarks",
                table: "loyalty_ledger");

            migrationBuilder.DropColumn(
                name: "address",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "average_basket_value",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "customer_segment",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "email",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "enrollment_date",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "last_points_earned_date",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "last_purchase_date",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "last_redemption_date",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "lifetime_points_earned",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "lifetime_spend",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "membership_status",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "preferred_category",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "visit_frequency",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "benefits_json",
                table: "customer_tiers");

            migrationBuilder.DropColumn(
                name: "minimum_points",
                table: "customer_tiers");

            migrationBuilder.DropColumn(
                name: "tier_downgrade_rule",
                table: "customer_tiers");

            migrationBuilder.DropColumn(
                name: "tier_upgrade_rule",
                table: "customer_tiers");

            migrationBuilder.RenameColumn(
                name: "previous_balance",
                table: "loyalty_ledger",
                newName: "running_points");

            migrationBuilder.RenameColumn(
                name: "points_redeemed",
                table: "loyalty_ledger",
                newName: "points");
        }
    }
}
