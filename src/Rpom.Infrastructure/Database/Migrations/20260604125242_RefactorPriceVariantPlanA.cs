using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rpom.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RefactorPriceVariantPlanA : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_price_variant_active_priority",
                schema: "public",
                table: "price_variants");

            migrationBuilder.DropColumn(
                name: "begin_date",
                schema: "public",
                table: "price_variants");

            migrationBuilder.DropColumn(
                name: "days_of_week",
                schema: "public",
                table: "price_variants");

            migrationBuilder.DropColumn(
                name: "end_date",
                schema: "public",
                table: "price_variants");

            migrationBuilder.DropColumn(
                name: "priority",
                schema: "public",
                table: "price_variants");

            migrationBuilder.AddColumn<int>(
                name: "day_mask",
                schema: "public",
                table: "price_variants",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_price_variant_active",
                schema: "public",
                table: "price_variants",
                columns: new[] { "price_table_id", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_price_variant_active",
                schema: "public",
                table: "price_variants");

            migrationBuilder.DropColumn(
                name: "day_mask",
                schema: "public",
                table: "price_variants");

            migrationBuilder.AddColumn<DateOnly>(
                name: "begin_date",
                schema: "public",
                table: "price_variants",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "days_of_week",
                schema: "public",
                table: "price_variants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "end_date",
                schema: "public",
                table: "price_variants",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "priority",
                schema: "public",
                table: "price_variants",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.CreateIndex(
                name: "ix_price_variant_active_priority",
                schema: "public",
                table: "price_variants",
                columns: new[] { "price_table_id", "is_active", "priority" });
        }
    }
}
