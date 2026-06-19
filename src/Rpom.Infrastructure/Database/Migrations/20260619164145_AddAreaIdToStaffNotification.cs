using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rpom.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAreaIdToStaffNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "area_id",
                schema: "public",
                table: "staff_notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_staff_notifications_area_id",
                schema: "public",
                table: "staff_notifications",
                column: "area_id");

            migrationBuilder.AddForeignKey(
                name: "fk_staff_notifications_areas_area_id",
                schema: "public",
                table: "staff_notifications",
                column: "area_id",
                principalSchema: "public",
                principalTable: "areas",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_staff_notifications_areas_area_id",
                schema: "public",
                table: "staff_notifications");

            migrationBuilder.DropIndex(
                name: "ix_staff_notifications_area_id",
                schema: "public",
                table: "staff_notifications");

            migrationBuilder.DropColumn(
                name: "area_id",
                schema: "public",
                table: "staff_notifications");
        }
    }
}
