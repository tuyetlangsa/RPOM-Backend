using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rpom.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSetComponentKitchenLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "done_at",
                schema: "public",
                table: "order_item_details",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "kitchen_station_id",
                schema: "public",
                table: "order_item_details",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ready_at",
                schema: "public",
                table: "order_item_details",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "start_cook_at",
                schema: "public",
                table: "order_item_details",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "public",
                table: "order_item_details",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "PENDING");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                schema: "public",
                table: "order_item_details",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.CreateIndex(
                name: "ix_order_item_detail_station_status",
                schema: "public",
                table: "order_item_details",
                columns: new[] { "kitchen_station_id", "status" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_order_item_detail_status",
                schema: "public",
                table: "order_item_details",
                sql: "status IN ('PENDING', 'PROCESSING', 'READY', 'DONE', 'CANCELLED')");

            migrationBuilder.AddForeignKey(
                name: "fk_order_item_details_kitchen_stations_kitchen_station_id",
                schema: "public",
                table: "order_item_details",
                column: "kitchen_station_id",
                principalSchema: "public",
                principalTable: "kitchen_stations",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_order_item_details_kitchen_stations_kitchen_station_id",
                schema: "public",
                table: "order_item_details");

            migrationBuilder.DropIndex(
                name: "ix_order_item_detail_station_status",
                schema: "public",
                table: "order_item_details");

            migrationBuilder.DropCheckConstraint(
                name: "ck_order_item_detail_status",
                schema: "public",
                table: "order_item_details");

            migrationBuilder.DropColumn(
                name: "done_at",
                schema: "public",
                table: "order_item_details");

            migrationBuilder.DropColumn(
                name: "kitchen_station_id",
                schema: "public",
                table: "order_item_details");

            migrationBuilder.DropColumn(
                name: "ready_at",
                schema: "public",
                table: "order_item_details");

            migrationBuilder.DropColumn(
                name: "start_cook_at",
                schema: "public",
                table: "order_item_details");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "public",
                table: "order_item_details");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "public",
                table: "order_item_details");
        }
    }
}
