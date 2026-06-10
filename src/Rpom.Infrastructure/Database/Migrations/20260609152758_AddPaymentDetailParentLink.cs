using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rpom.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentDetailParentLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "parent_payment_detail_id",
                schema: "public",
                table: "ticket_payment_details",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticket_payment_details_parent_payment_detail_id",
                schema: "public",
                table: "ticket_payment_details",
                column: "parent_payment_detail_id");

            migrationBuilder.AddForeignKey(
                name: "fk_ticket_payment_details_ticket_payment_details_parent_paymen",
                schema: "public",
                table: "ticket_payment_details",
                column: "parent_payment_detail_id",
                principalSchema: "public",
                principalTable: "ticket_payment_details",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ticket_payment_details_ticket_payment_details_parent_paymen",
                schema: "public",
                table: "ticket_payment_details");

            migrationBuilder.DropIndex(
                name: "ix_ticket_payment_details_parent_payment_detail_id",
                schema: "public",
                table: "ticket_payment_details");

            migrationBuilder.DropColumn(
                name: "parent_payment_detail_id",
                schema: "public",
                table: "ticket_payment_details");
        }
    }
}
