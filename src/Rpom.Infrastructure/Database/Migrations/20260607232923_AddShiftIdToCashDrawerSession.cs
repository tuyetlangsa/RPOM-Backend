using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rpom.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftIdToCashDrawerSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "shift_id",
                schema: "public",
                table: "cash_drawer_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_cash_drawer_sessions_shift_id",
                schema: "public",
                table: "cash_drawer_sessions",
                column: "shift_id");

            migrationBuilder.AddForeignKey(
                name: "fk_cash_drawer_sessions_shifts_shift_id",
                schema: "public",
                table: "cash_drawer_sessions",
                column: "shift_id",
                principalSchema: "public",
                principalTable: "shifts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_cash_drawer_sessions_shifts_shift_id",
                schema: "public",
                table: "cash_drawer_sessions");

            migrationBuilder.DropIndex(
                name: "ix_cash_drawer_sessions_shift_id",
                schema: "public",
                table: "cash_drawer_sessions");

            migrationBuilder.DropColumn(
                name: "shift_id",
                schema: "public",
                table: "cash_drawer_sessions");
        }
    }
}
