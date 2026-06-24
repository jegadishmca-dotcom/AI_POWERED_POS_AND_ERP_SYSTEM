using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PosErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase5AiEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "severity",
                table: "ai_alerts",
                newName: "alert_severity");

            migrationBuilder.CreateTable(
                name: "ai_business_insights",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    insight_category = table.Column<string>(type: "text", nullable: false),
                    business_area = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    impact_score = table.Column<int>(type: "integer", nullable: false),
                    confidence_score = table.Column<int>(type: "integer", nullable: false),
                    estimated_financial_impact = table.Column<decimal>(type: "numeric", nullable: false),
                    recommended_action = table.Column<string>(type: "text", nullable: false),
                    generation_reasoning = table.Column<string>(type: "text", nullable: false),
                    reference_type = table.Column<string>(type: "text", nullable: true),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    assigned_to = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolution_notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_business_insights", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_customer_intelligences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    segment_type = table.Column<string>(type: "text", nullable: false),
                    churn_risk_pct = table.Column<decimal>(type: "numeric", nullable: false),
                    ltv_prediction = table.Column<decimal>(type: "numeric", nullable: false),
                    lifetime_value_category = table.Column<string>(type: "text", nullable: false),
                    predicted_next_purchase_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    churn_category = table.Column<string>(type: "text", nullable: false),
                    recommended_action = table.Column<string>(type: "text", nullable: false),
                    last_calculated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_customer_intelligences", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_demand_forecasts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    forecast_type = table.Column<string>(type: "text", nullable: false),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    forecast_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    forecast_horizon_days = table.Column<int>(type: "integer", nullable: false),
                    forecast_method = table.Column<string>(type: "text", nullable: false),
                    forecast_quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    actual_quantity = table.Column<decimal>(type: "numeric", nullable: true),
                    forecast_error = table.Column<decimal>(type: "numeric", nullable: true),
                    confidence_level = table.Column<decimal>(type: "numeric", nullable: false),
                    model_version = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_demand_forecasts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_store_performances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metric_name = table.Column<string>(type: "text", nullable: false),
                    metric_value = table.Column<decimal>(type: "numeric", nullable: false),
                    benchmark_value = table.Column<decimal>(type: "numeric", nullable: false),
                    variance = table.Column<decimal>(type: "numeric", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    benchmark_group = table.Column<string>(type: "text", nullable: false),
                    percentile = table.Column<decimal>(type: "numeric", nullable: false),
                    calculated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_store_performances", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "executive_kpi_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    daily_sales = table.Column<decimal>(type: "numeric", nullable: false),
                    daily_profit = table.Column<decimal>(type: "numeric", nullable: false),
                    gross_margin_pct = table.Column<decimal>(type: "numeric", nullable: false),
                    total_inventory_value = table.Column<decimal>(type: "numeric", nullable: false),
                    dead_stock_value = table.Column<decimal>(type: "numeric", nullable: false),
                    active_loyalty_members = table.Column<int>(type: "integer", nullable: false),
                    active_customers = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_executive_kpi_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "forecast_accuracy_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    model_version = table.Column<string>(type: "text", nullable: false),
                    mean_absolute_percentage_error = table.Column<decimal>(type: "numeric", nullable: false),
                    mean_absolute_error = table.Column<decimal>(type: "numeric", nullable: false),
                    root_mean_square_error = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_forecast_accuracy_snapshots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_product_id",
                table: "stock_ledger",
                column: "product_id");

            migrationBuilder.AddForeignKey(
                name: "FK_stock_ledger_products_product_id",
                table: "stock_ledger",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stock_ledger_products_product_id",
                table: "stock_ledger");

            migrationBuilder.DropTable(
                name: "ai_business_insights");

            migrationBuilder.DropTable(
                name: "ai_customer_intelligences");

            migrationBuilder.DropTable(
                name: "ai_demand_forecasts");

            migrationBuilder.DropTable(
                name: "ai_store_performances");

            migrationBuilder.DropTable(
                name: "executive_kpi_snapshots");

            migrationBuilder.DropTable(
                name: "forecast_accuracy_snapshots");

            migrationBuilder.DropIndex(
                name: "IX_stock_ledger_product_id",
                table: "stock_ledger");

            migrationBuilder.RenameColumn(
                name: "alert_severity",
                table: "ai_alerts",
                newName: "severity");
        }
    }
}
