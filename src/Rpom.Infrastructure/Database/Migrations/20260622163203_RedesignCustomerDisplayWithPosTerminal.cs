using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Rpom.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RedesignCustomerDisplayWithPosTerminal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_customer_displays_counters_counter_id",
                schema: "public",
                table: "customer_displays");

            migrationBuilder.DropForeignKey(
                name: "fk_customer_displays_staff_accounts_paired_staff_account_id",
                schema: "public",
                table: "customer_displays");

            migrationBuilder.DropIndex(
                name: "ix_customer_display_counter",
                schema: "public",
                table: "customer_displays");

            migrationBuilder.DropIndex(
                name: "ix_customer_displays_paired_staff_account_id",
                schema: "public",
                table: "customer_displays");

            migrationBuilder.DropIndex(
                name: "ix_customer_displays_pairing_code",
                schema: "public",
                table: "customer_displays");

            migrationBuilder.DropColumn(
                name: "paired_staff_account_id",
                schema: "public",
                table: "customer_displays");

            migrationBuilder.DropColumn(
                name: "pairing_code",
                schema: "public",
                table: "customer_displays");

            migrationBuilder.RenameColumn(
                name: "paired_at",
                schema: "public",
                table: "customer_displays",
                newName: "activated_at");

            migrationBuilder.RenameColumn(
                name: "counter_id",
                schema: "public",
                table: "customer_displays",
                newName: "pos_terminal_id");

            migrationBuilder.AddColumn<int>(
                name: "pos_terminal_id",
                schema: "public",
                table: "ticket_payment_details",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "activated_client_id",
                schema: "public",
                table: "customer_displays",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pos_terminals",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    counter_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    device_token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pos_terminals", x => x.id);
                    table.ForeignKey(
                        name: "fk_pos_terminals_counters_counter_id",
                        column: x => x.counter_id,
                        principalSchema: "public",
                        principalTable: "counters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ticket_payment_details_pos_terminal_id",
                schema: "public",
                table: "ticket_payment_details",
                column: "pos_terminal_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_displays_pos_terminal_id",
                schema: "public",
                table: "customer_displays",
                column: "pos_terminal_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pos_terminal_counter",
                schema: "public",
                table: "pos_terminals",
                column: "counter_id");

            migrationBuilder.CreateIndex(
                name: "ix_pos_terminals_device_token",
                schema: "public",
                table: "pos_terminals",
                column: "device_token",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_customer_displays_pos_terminals_pos_terminal_id",
                schema: "public",
                table: "customer_displays",
                column: "pos_terminal_id",
                principalSchema: "public",
                principalTable: "pos_terminals",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_ticket_payment_details_pos_terminals_pos_terminal_id",
                schema: "public",
                table: "ticket_payment_details",
                column: "pos_terminal_id",
                principalSchema: "public",
                principalTable: "pos_terminals",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_customer_displays_pos_terminals_pos_terminal_id",
                schema: "public",
                table: "customer_displays");

            migrationBuilder.DropForeignKey(
                name: "fk_ticket_payment_details_pos_terminals_pos_terminal_id",
                schema: "public",
                table: "ticket_payment_details");

            migrationBuilder.DropTable(
                name: "pos_terminals",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "ix_ticket_payment_details_pos_terminal_id",
                schema: "public",
                table: "ticket_payment_details");

            migrationBuilder.DropIndex(
                name: "ix_customer_displays_pos_terminal_id",
                schema: "public",
                table: "customer_displays");

            migrationBuilder.DropColumn(
                name: "pos_terminal_id",
                schema: "public",
                table: "ticket_payment_details");

            migrationBuilder.DropColumn(
                name: "activated_client_id",
                schema: "public",
                table: "customer_displays");

            migrationBuilder.RenameColumn(
                name: "pos_terminal_id",
                schema: "public",
                table: "customer_displays",
                newName: "counter_id");

            migrationBuilder.RenameColumn(
                name: "activated_at",
                schema: "public",
                table: "customer_displays",
                newName: "paired_at");

            migrationBuilder.AddColumn<int>(
                name: "paired_staff_account_id",
                schema: "public",
                table: "customer_displays",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pairing_code",
                schema: "public",
                table: "customer_displays",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_customer_display_counter",
                schema: "public",
                table: "customer_displays",
                column: "counter_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_displays_paired_staff_account_id",
                schema: "public",
                table: "customer_displays",
                column: "paired_staff_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_displays_pairing_code",
                schema: "public",
                table: "customer_displays",
                column: "pairing_code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_customer_displays_counters_counter_id",
                schema: "public",
                table: "customer_displays",
                column: "counter_id",
                principalSchema: "public",
                principalTable: "counters",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_customer_displays_staff_accounts_paired_staff_account_id",
                schema: "public",
                table: "customer_displays",
                column: "paired_staff_account_id",
                principalSchema: "public",
                principalTable: "staff_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
