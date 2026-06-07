using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rpom.Infrastructure.Database.Migrations;

/// <inheritdoc />
public partial class PricingBillingFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_ticket_item_sum_bucket",
            schema: "public",
            table: "ticket_item_sums");

        migrationBuilder.RenameColumn(
            name: "total_choice_amount",
            schema: "public",
            table: "ticket_item_sums",
            newName: "total_service_charge");

        migrationBuilder.RenameColumn(
            name: "subtotal",
            schema: "public",
            table: "ticket_item_sums",
            newName: "total_line_subtotal");

        migrationBuilder.RenameColumn(
            name: "discount_percent",
            schema: "public",
            table: "ticket_item_sums",
            newName: "ticket_discount_percent");

        migrationBuilder.AddColumn<decimal>(
            name: "discount_percent",
            schema: "public",
            table: "tickets",
            type: "numeric(5,2)",
            precision: 5,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "line_discount_total",
            schema: "public",
            table: "tickets",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "refund_amount",
            schema: "public",
            table: "tickets",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "rounding_adjustment",
            schema: "public",
            table: "tickets",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "service_charge_vat_percent",
            schema: "public",
            table: "tickets",
            type: "numeric(5,2)",
            precision: 5,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "ticket_discount_total",
            schema: "public",
            table: "tickets",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "line_discount_percent",
            schema: "public",
            table: "ticket_item_sums",
            type: "numeric(5,2)",
            precision: 5,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "service_charge_vat_percent",
            schema: "public",
            table: "ticket_item_sums",
            type: "numeric(5,2)",
            precision: 5,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "choice_price_per_unit",
            schema: "public",
            table: "order_items",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "line_discount_amount",
            schema: "public",
            table: "order_items",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "line_discount_percent",
            schema: "public",
            table: "order_items",
            type: "numeric(5,2)",
            precision: 5,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "line_subtotal",
            schema: "public",
            table: "order_items",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "service_charge_amount",
            schema: "public",
            table: "order_items",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "service_charge_percent",
            schema: "public",
            table: "order_items",
            type: "numeric(5,2)",
            precision: 5,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "service_charge_vat_percent",
            schema: "public",
            table: "order_items",
            type: "numeric(5,2)",
            precision: 5,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "ticket_discount_amount",
            schema: "public",
            table: "order_items",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "ticket_discount_percent",
            schema: "public",
            table: "order_items",
            type: "numeric(5,2)",
            precision: 5,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "total_discount_amount",
            schema: "public",
            table: "order_items",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "vat_amount",
            schema: "public",
            table: "order_items",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "vat_item_amount",
            schema: "public",
            table: "order_items",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "vat_percent",
            schema: "public",
            table: "order_items",
            type: "numeric(5,2)",
            precision: 5,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "vat_sc_amount",
            schema: "public",
            table: "order_items",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "choice_price_per_unit",
            schema: "public",
            table: "cart_items",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "line_subtotal",
            schema: "public",
            table: "cart_items",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "service_charge_amount",
            schema: "public",
            table: "cart_items",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "service_charge_percent",
            schema: "public",
            table: "cart_items",
            type: "numeric(5,2)",
            precision: 5,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "service_charge_vat_percent",
            schema: "public",
            table: "cart_items",
            type: "numeric(5,2)",
            precision: 5,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "vat_amount",
            schema: "public",
            table: "cart_items",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "vat_item_amount",
            schema: "public",
            table: "cart_items",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "vat_percent",
            schema: "public",
            table: "cart_items",
            type: "numeric(5,2)",
            precision: 5,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "vat_sc_amount",
            schema: "public",
            table: "cart_items",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "service_charge_percent",
            schema: "public",
            table: "areas",
            type: "numeric(5,2)",
            precision: 5,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "service_charge_vat_percent",
            schema: "public",
            table: "areas",
            type: "numeric(5,2)",
            precision: 5,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.CreateTable(
            name: "rounding_configs",
            schema: "public",
            columns: table => new
            {
                key_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                digits = table.Column<short>(type: "smallint", nullable: false),
                description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_rounding_configs", x => x.key_code);
            });

        migrationBuilder.CreateIndex(
            name: "ux_ticket_item_sum_bucket",
            schema: "public",
            table: "ticket_item_sums",
            columns: new[] { "ticket_id", "item_id", "uom_id", "unit_price", "choice_price_per_unit", "line_discount_percent", "ticket_discount_percent", "vat_percent", "service_charge_percent", "service_charge_vat_percent" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "rounding_configs",
            schema: "public");

        migrationBuilder.DropIndex(
            name: "ux_ticket_item_sum_bucket",
            schema: "public",
            table: "ticket_item_sums");

        migrationBuilder.DropColumn(
            name: "discount_percent",
            schema: "public",
            table: "tickets");

        migrationBuilder.DropColumn(
            name: "line_discount_total",
            schema: "public",
            table: "tickets");

        migrationBuilder.DropColumn(
            name: "refund_amount",
            schema: "public",
            table: "tickets");

        migrationBuilder.DropColumn(
            name: "rounding_adjustment",
            schema: "public",
            table: "tickets");

        migrationBuilder.DropColumn(
            name: "service_charge_vat_percent",
            schema: "public",
            table: "tickets");

        migrationBuilder.DropColumn(
            name: "ticket_discount_total",
            schema: "public",
            table: "tickets");

        migrationBuilder.DropColumn(
            name: "line_discount_percent",
            schema: "public",
            table: "ticket_item_sums");

        migrationBuilder.DropColumn(
            name: "service_charge_vat_percent",
            schema: "public",
            table: "ticket_item_sums");

        migrationBuilder.DropColumn(
            name: "choice_price_per_unit",
            schema: "public",
            table: "order_items");

        migrationBuilder.DropColumn(
            name: "line_discount_amount",
            schema: "public",
            table: "order_items");

        migrationBuilder.DropColumn(
            name: "line_discount_percent",
            schema: "public",
            table: "order_items");

        migrationBuilder.DropColumn(
            name: "line_subtotal",
            schema: "public",
            table: "order_items");

        migrationBuilder.DropColumn(
            name: "service_charge_amount",
            schema: "public",
            table: "order_items");

        migrationBuilder.DropColumn(
            name: "service_charge_percent",
            schema: "public",
            table: "order_items");

        migrationBuilder.DropColumn(
            name: "service_charge_vat_percent",
            schema: "public",
            table: "order_items");

        migrationBuilder.DropColumn(
            name: "ticket_discount_amount",
            schema: "public",
            table: "order_items");

        migrationBuilder.DropColumn(
            name: "ticket_discount_percent",
            schema: "public",
            table: "order_items");

        migrationBuilder.DropColumn(
            name: "total_discount_amount",
            schema: "public",
            table: "order_items");

        migrationBuilder.DropColumn(
            name: "vat_amount",
            schema: "public",
            table: "order_items");

        migrationBuilder.DropColumn(
            name: "vat_item_amount",
            schema: "public",
            table: "order_items");

        migrationBuilder.DropColumn(
            name: "vat_percent",
            schema: "public",
            table: "order_items");

        migrationBuilder.DropColumn(
            name: "vat_sc_amount",
            schema: "public",
            table: "order_items");

        migrationBuilder.DropColumn(
            name: "choice_price_per_unit",
            schema: "public",
            table: "cart_items");

        migrationBuilder.DropColumn(
            name: "line_subtotal",
            schema: "public",
            table: "cart_items");

        migrationBuilder.DropColumn(
            name: "service_charge_amount",
            schema: "public",
            table: "cart_items");

        migrationBuilder.DropColumn(
            name: "service_charge_percent",
            schema: "public",
            table: "cart_items");

        migrationBuilder.DropColumn(
            name: "service_charge_vat_percent",
            schema: "public",
            table: "cart_items");

        migrationBuilder.DropColumn(
            name: "vat_amount",
            schema: "public",
            table: "cart_items");

        migrationBuilder.DropColumn(
            name: "vat_item_amount",
            schema: "public",
            table: "cart_items");

        migrationBuilder.DropColumn(
            name: "vat_percent",
            schema: "public",
            table: "cart_items");

        migrationBuilder.DropColumn(
            name: "vat_sc_amount",
            schema: "public",
            table: "cart_items");

        migrationBuilder.DropColumn(
            name: "service_charge_percent",
            schema: "public",
            table: "areas");

        migrationBuilder.DropColumn(
            name: "service_charge_vat_percent",
            schema: "public",
            table: "areas");

        migrationBuilder.RenameColumn(
            name: "total_service_charge",
            schema: "public",
            table: "ticket_item_sums",
            newName: "total_choice_amount");

        migrationBuilder.RenameColumn(
            name: "total_line_subtotal",
            schema: "public",
            table: "ticket_item_sums",
            newName: "subtotal");

        migrationBuilder.RenameColumn(
            name: "ticket_discount_percent",
            schema: "public",
            table: "ticket_item_sums",
            newName: "discount_percent");

        migrationBuilder.CreateIndex(
            name: "ux_ticket_item_sum_bucket",
            schema: "public",
            table: "ticket_item_sums",
            columns: new[] { "ticket_id", "item_id", "uom_id", "unit_price", "discount_percent", "choice_price_per_unit", "vat_percent", "service_charge_percent" },
            unique: true);
    }
}
