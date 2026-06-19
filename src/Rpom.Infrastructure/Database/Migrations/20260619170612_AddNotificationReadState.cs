using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rpom.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationReadState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_read_states",
                schema: "public",
                columns: table => new
                {
                    staff_account_id = table.Column<int>(type: "integer", nullable: false),
                    counter_id = table.Column<int>(type: "integer", nullable: false),
                    last_read_notification_id = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_read_states", x => new { x.staff_account_id, x.counter_id });
                    table.ForeignKey(
                        name: "fk_notification_read_states_counters_counter_id",
                        column: x => x.counter_id,
                        principalSchema: "public",
                        principalTable: "counters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notification_read_states_staff_accounts_staff_account_id",
                        column: x => x.staff_account_id,
                        principalSchema: "public",
                        principalTable: "staff_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notification_read_states_counter_id",
                schema: "public",
                table: "notification_read_states",
                column: "counter_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_read_states",
                schema: "public");
        }
    }
}
