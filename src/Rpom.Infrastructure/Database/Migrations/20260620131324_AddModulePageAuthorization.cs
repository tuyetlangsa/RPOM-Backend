using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Rpom.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddModulePageAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "modules",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pages",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    module_id = table.Column<int>(type: "integer", nullable: false),
                    display_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pages", x => x.id);
                    table.ForeignKey(
                        name: "fk_pages_modules_module_id",
                        column: x => x.module_id,
                        principalSchema: "public",
                        principalTable: "modules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staff_account_page_accesses",
                schema: "public",
                columns: table => new
                {
                    staff_account_id = table.Column<int>(type: "integer", nullable: false),
                    page_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_account_page_accesses", x => new { x.staff_account_id, x.page_id });
                    table.ForeignKey(
                        name: "fk_staff_account_page_accesses_pages_page_id",
                        column: x => x.page_id,
                        principalSchema: "public",
                        principalTable: "pages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_staff_account_page_accesses_staff_accounts_staff_account_id",
                        column: x => x.staff_account_id,
                        principalSchema: "public",
                        principalTable: "staff_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_modules_code",
                schema: "public",
                table: "modules",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pages_code",
                schema: "public",
                table: "pages",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pages_module_id",
                schema: "public",
                table: "pages",
                column: "module_id");

            migrationBuilder.CreateIndex(
                name: "ix_staff_account_page_access_page_id",
                schema: "public",
                table: "staff_account_page_accesses",
                column: "page_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_account_page_accesses",
                schema: "public");

            migrationBuilder.DropTable(
                name: "pages",
                schema: "public");

            migrationBuilder.DropTable(
                name: "modules",
                schema: "public");
        }
    }
}
