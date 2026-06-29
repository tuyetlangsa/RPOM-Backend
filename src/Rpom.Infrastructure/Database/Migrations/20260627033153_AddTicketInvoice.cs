using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Rpom.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ticket_invoices",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ticket_id = table.Column<long>(type: "bigint", nullable: false),
                    ticket_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    counter_id = table.Column<int>(type: "integer", nullable: false),
                    area_id = table.Column<int>(type: "integer", nullable: false),
                    shift_id = table.Column<int>(type: "integer", nullable: false),
                    table_id = table.Column<int>(type: "integer", nullable: false),
                    table_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    guest_count = table.Column<short>(type: "smallint", nullable: false),
                    waiter_staff_id = table.Column<int>(type: "integer", nullable: true),
                    waiter_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    closed_by_staff_id = table.Column<int>(type: "integer", nullable: true),
                    closed_by_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    discount_percent = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false, defaultValue: 0m),
                    service_charge_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    service_charge_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    rounding_adjustment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    paid_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    refund_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    change_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    opened_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_invoices", x => x.id);
                    table.ForeignKey(
                        name: "fk_ticket_invoices_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalSchema: "public",
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ticket_invoice_lines",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ticket_invoice_id = table.Column<long>(type: "bigint", nullable: false),
                    item_id = table.Column<int>(type: "integer", nullable: false),
                    item_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    item_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    uom_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    uom_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    choice_price_per_unit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                    service_charge_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                    service_charge_vat_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                    line_discount_percent = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false, defaultValue: 0m),
                    ticket_discount_percent = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false, defaultValue: 0m),
                    line_subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_discount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    service_charge_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_invoice_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_ticket_invoice_lines_ticket_invoices_ticket_invoice_id",
                        column: x => x.ticket_invoice_id,
                        principalSchema: "public",
                        principalTable: "ticket_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ticket_invoice_lines_ticket_invoice_id",
                schema: "public",
                table: "ticket_invoice_lines",
                column: "ticket_invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_invoices_closed_at",
                schema: "public",
                table: "ticket_invoices",
                column: "closed_at");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_invoices_counter_id_closed_at",
                schema: "public",
                table: "ticket_invoices",
                columns: new[] { "counter_id", "closed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ticket_invoices_shift_id_closed_at",
                schema: "public",
                table: "ticket_invoices",
                columns: new[] { "shift_id", "closed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ticket_invoices_ticket_id",
                schema: "public",
                table: "ticket_invoices",
                column: "ticket_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_invoice_lines",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ticket_invoices",
                schema: "public");
        }
    }
}
