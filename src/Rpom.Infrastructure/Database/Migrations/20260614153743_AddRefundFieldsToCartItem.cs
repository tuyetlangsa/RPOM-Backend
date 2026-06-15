using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rpom.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundFieldsToCartItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cancellation_note",
                schema: "public",
                table: "cart_items",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cancellation_reason_id",
                schema: "public",
                table: "cart_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "original_order_item_id",
                schema: "public",
                table: "cart_items",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cancellation_note",
                schema: "public",
                table: "cart_items");

            migrationBuilder.DropColumn(
                name: "cancellation_reason_id",
                schema: "public",
                table: "cart_items");

            migrationBuilder.DropColumn(
                name: "original_order_item_id",
                schema: "public",
                table: "cart_items");
        }
    }
}
