using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Rpom.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerDisplayAndQrExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "expires_at",
                schema: "public",
                table: "ticket_payment_details",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "customer_displays",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    counter_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    device_token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    pairing_code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    paired_staff_account_id = table.Column<int>(type: "integer", nullable: true),
                    paired_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    idle_media_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_displays", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_displays_counters_counter_id",
                        column: x => x.counter_id,
                        principalSchema: "public",
                        principalTable: "counters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_customer_displays_staff_accounts_paired_staff_account_id",
                        column: x => x.paired_staff_account_id,
                        principalSchema: "public",
                        principalTable: "staff_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customer_display_counter",
                schema: "public",
                table: "customer_displays",
                column: "counter_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_displays_device_token",
                schema: "public",
                table: "customer_displays",
                column: "device_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customer_displays_paired_staff_account_id",
                schema: "public",
                table: "customer_displays",
                column: "paired_staff_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_displays_pairing_code",
                schema: "public",
                table: "customer_displays",
                column: "pairing_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_displays",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "expires_at",
                schema: "public",
                table: "ticket_payment_details");
        }
    }
}
