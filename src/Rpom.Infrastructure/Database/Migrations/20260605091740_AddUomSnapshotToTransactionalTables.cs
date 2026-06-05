using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rpom.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddUomSnapshotToTransactionalTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "uom_code",
                schema: "public",
                table: "ticket_item_sums",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "uom_name",
                schema: "public",
                table: "ticket_item_sums",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "uom_code",
                schema: "public",
                table: "order_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "uom_name",
                schema: "public",
                table: "order_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "uom_code",
                schema: "public",
                table: "cart_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "uom_name",
                schema: "public",
                table: "cart_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "uom_code",
                schema: "public",
                table: "ticket_item_sums");

            migrationBuilder.DropColumn(
                name: "uom_name",
                schema: "public",
                table: "ticket_item_sums");

            migrationBuilder.DropColumn(
                name: "uom_code",
                schema: "public",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "uom_name",
                schema: "public",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "uom_code",
                schema: "public",
                table: "cart_items");

            migrationBuilder.DropColumn(
                name: "uom_name",
                schema: "public",
                table: "cart_items");
        }
    }
}
