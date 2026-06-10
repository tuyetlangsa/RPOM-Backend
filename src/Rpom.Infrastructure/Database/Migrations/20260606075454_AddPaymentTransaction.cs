using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Rpom.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_transactions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    gateway = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "SEPAY"),
                    gateway_transaction_id = table.Column<long>(type: "bigint", nullable: false),
                    bank_brand = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    account_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    sub_account = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    transfer_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    transfer_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    accumulated = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    content = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    reference_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    transaction_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    raw_payload = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "UNMATCHED"),
                    matched_reference_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    matched_payment_detail_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_transactions", x => x.id);
                    table.CheckConstraint("ck_payment_transaction_status", "status IN ('MATCHED', 'UNMATCHED', 'MISMATCH', 'DUPLICATE', 'IGNORED')");
                    table.ForeignKey(
                        name: "fk_payment_transactions_ticket_payment_details_matched_payment",
                        column: x => x.matched_payment_detail_id,
                        principalSchema: "public",
                        principalTable: "ticket_payment_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payment_transactions_matched_payment_detail_id",
                schema: "public",
                table: "payment_transactions",
                column: "matched_payment_detail_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_transactions_status",
                schema: "public",
                table: "payment_transactions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_payment_transaction_gateway_tx_id",
                schema: "public",
                table: "payment_transactions",
                column: "gateway_transaction_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_transactions",
                schema: "public");
        }
    }
}
