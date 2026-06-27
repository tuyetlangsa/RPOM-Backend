using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rpom.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationRedesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_reservations_tables_table_id",
                schema: "public",
                table: "reservations");

            migrationBuilder.DropForeignKey(
                name: "fk_reservations_tickets_linked_ticket_id",
                schema: "public",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "ix_reservation_status_target_time",
                schema: "public",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "ix_reservations_linked_ticket_id",
                schema: "public",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "ix_reservations_table_id",
                schema: "public",
                table: "reservations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reservation_status",
                schema: "public",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "linked_ticket_id",
                schema: "public",
                table: "reservations");

            migrationBuilder.RenameColumn(
                name: "table_id",
                schema: "public",
                table: "reservations",
                newName: "counter_id");

            migrationBuilder.RenameIndex(
                name: "ix_reservation_table_active",
                schema: "public",
                table: "reservations",
                newName: "ix_reservation_counter_status_target_time");

            migrationBuilder.AddColumn<long>(
                name: "reservation_id",
                schema: "public",
                table: "tickets",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version",
                schema: "public",
                table: "reservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "reservation_tables",
                schema: "public",
                columns: table => new
                {
                    reservation_id = table.Column<long>(type: "bigint", nullable: false),
                    table_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reservation_tables", x => new { x.reservation_id, x.table_id });
                    table.ForeignKey(
                        name: "fk_reservation_tables_reservations_reservation_id",
                        column: x => x.reservation_id,
                        principalSchema: "public",
                        principalTable: "reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reservation_tables_tables_table_id",
                        column: x => x.table_id,
                        principalSchema: "public",
                        principalTable: "tables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ticket_reservation_id",
                schema: "public",
                table: "tickets",
                column: "reservation_id");

            migrationBuilder.CreateIndex(
                name: "ix_reservation_updated_at",
                schema: "public",
                table: "reservations",
                column: "updated_at");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reservation_status",
                schema: "public",
                table: "reservations",
                sql: "status IN ('BOOKED', 'ARRIVED', 'CANCELLED', 'NOT_ARRIVED')");

            migrationBuilder.CreateIndex(
                name: "ix_reservation_table_table_id",
                schema: "public",
                table: "reservation_tables",
                column: "table_id");

            migrationBuilder.AddForeignKey(
                name: "fk_reservations_counters_counter_id",
                schema: "public",
                table: "reservations",
                column: "counter_id",
                principalSchema: "public",
                principalTable: "counters",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_tickets_reservations_reservation_id",
                schema: "public",
                table: "tickets",
                column: "reservation_id",
                principalSchema: "public",
                principalTable: "reservations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_reservations_counters_counter_id",
                schema: "public",
                table: "reservations");

            migrationBuilder.DropForeignKey(
                name: "fk_tickets_reservations_reservation_id",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropTable(
                name: "reservation_tables",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "ix_ticket_reservation_id",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_reservation_updated_at",
                schema: "public",
                table: "reservations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reservation_status",
                schema: "public",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "reservation_id",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "version",
                schema: "public",
                table: "reservations");

            migrationBuilder.RenameColumn(
                name: "counter_id",
                schema: "public",
                table: "reservations",
                newName: "table_id");

            migrationBuilder.RenameIndex(
                name: "ix_reservation_counter_status_target_time",
                schema: "public",
                table: "reservations",
                newName: "ix_reservation_table_active");

            migrationBuilder.AddColumn<long>(
                name: "linked_ticket_id",
                schema: "public",
                table: "reservations",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_reservation_status_target_time",
                schema: "public",
                table: "reservations",
                columns: new[] { "status", "target_time" });

            migrationBuilder.CreateIndex(
                name: "ix_reservations_linked_ticket_id",
                schema: "public",
                table: "reservations",
                column: "linked_ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_reservations_table_id",
                schema: "public",
                table: "reservations",
                column: "table_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reservation_status",
                schema: "public",
                table: "reservations",
                sql: "status IN ('BOOKED', 'ARRIVED', 'CANCELLED')");

            migrationBuilder.AddForeignKey(
                name: "fk_reservations_tables_table_id",
                schema: "public",
                table: "reservations",
                column: "table_id",
                principalSchema: "public",
                principalTable: "tables",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_reservations_tickets_linked_ticket_id",
                schema: "public",
                table: "reservations",
                column: "linked_ticket_id",
                principalSchema: "public",
                principalTable: "tickets",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
