using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rpom.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigValueType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the column with a temporary default so existing rows are valid.
            migrationBuilder.AddColumn<string>(
                name: "value_type",
                schema: "public",
                table: "config_values",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "TEXT");

            // Backfill numeric codes; the four restaurant.* text codes stay TEXT.
            migrationBuilder.Sql(@"
                UPDATE public.config_values SET value_type = 'NUMBER'
                WHERE code IN (
                    'restaurant.vat_default_percent',
                    'restaurant.service_charge_default_percent',
                    'reservation.pre_buffer_minutes',
                    'reservation.grace_period_minutes',
                    'kitchen.late_threshold_minutes',
                    'printer.copies_default'
                );");

            // Drop the temporary default so future inserts must set value_type explicitly.
            migrationBuilder.AlterColumn<string>(
                name: "value_type",
                schema: "public",
                table: "config_values",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldDefaultValue: "TEXT");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "value_type",
                schema: "public",
                table: "config_values");
        }
    }
}
