using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rpom.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddTableLock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "table_locks",
                schema: "public",
                columns: table => new
                {
                    table_id = table.Column<int>(type: "integer", nullable: false),
                    staff_account_id = table.Column<int>(type: "integer", nullable: false),
                    staff_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    acquired_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_heartbeat_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_table_locks", x => x.table_id);
                    table.ForeignKey(
                        name: "fk_table_locks_tables_table_id",
                        column: x => x.table_id,
                        principalSchema: "public",
                        principalTable: "tables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_table_lock_staff",
                schema: "public",
                table: "table_locks",
                column: "staff_account_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "table_locks",
                schema: "public");
        }
    }
}
