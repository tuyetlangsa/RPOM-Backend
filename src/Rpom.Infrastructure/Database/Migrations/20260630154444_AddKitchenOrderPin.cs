using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rpom.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddKitchenOrderPin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "kitchen_order_pins",
                schema: "public",
                columns: table => new
                {
                    kitchen_station_id = table.Column<int>(type: "integer", nullable: false),
                    order_id = table.Column<long>(type: "bigint", nullable: false),
                    pinned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    pinned_by_staff_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kitchen_order_pins", x => new { x.kitchen_station_id, x.order_id });
                    table.ForeignKey(
                        name: "fk_kitchen_order_pins_kitchen_stations_kitchen_station_id",
                        column: x => x.kitchen_station_id,
                        principalSchema: "public",
                        principalTable: "kitchen_stations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_kitchen_order_pins_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "public",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_kitchen_order_pin_station",
                schema: "public",
                table: "kitchen_order_pins",
                column: "kitchen_station_id");

            migrationBuilder.CreateIndex(
                name: "ix_kitchen_order_pins_order_id",
                schema: "public",
                table: "kitchen_order_pins",
                column: "order_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kitchen_order_pins",
                schema: "public");
        }
    }
}
