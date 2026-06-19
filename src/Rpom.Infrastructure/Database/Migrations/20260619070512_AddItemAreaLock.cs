using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rpom.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddItemAreaLock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_available",
                schema: "public",
                table: "items");

            migrationBuilder.CreateTable(
                name: "item_area_locks",
                schema: "public",
                columns: table => new
                {
                    item_id = table.Column<int>(type: "integer", nullable: false),
                    area_id = table.Column<int>(type: "integer", nullable: false),
                    locked_by_staff_id = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    locked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_area_locks", x => new { x.item_id, x.area_id });
                    table.ForeignKey(
                        name: "fk_item_area_locks_areas_area_id",
                        column: x => x.area_id,
                        principalSchema: "public",
                        principalTable: "areas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_item_area_locks_items_item_id",
                        column: x => x.item_id,
                        principalSchema: "public",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_item_area_lock_area",
                schema: "public",
                table: "item_area_locks",
                column: "area_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "item_area_locks",
                schema: "public");

            migrationBuilder.AddColumn<bool>(
                name: "is_available",
                schema: "public",
                table: "items",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }
    }
}
