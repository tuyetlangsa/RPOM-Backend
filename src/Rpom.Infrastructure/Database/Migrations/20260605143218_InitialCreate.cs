using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

namespace Rpom.Infrastructure.Database.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "public");

        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:PostgresExtension:vector", ",,");

        migrationBuilder.CreateTable(
            name: "audit_logs",
            schema: "public",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                entity_id = table.Column<long>(type: "bigint", nullable: false),
                action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                actor_staff_account_id = table.Column<int>(type: "integer", nullable: true),
                actor_full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_audit_logs", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "cancellation_reasons",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                display_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_cancellation_reasons", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "categories",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                parent_id = table.Column<int>(type: "integer", nullable: true),
                path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                level = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                display_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_categories", x => x.id);
                table.ForeignKey(
                    name: "fk_categories_categories_parent_id",
                    column: x => x.parent_id,
                    principalSchema: "public",
                    principalTable: "categories",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "choice_categories",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                min_choice = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                max_choice = table.Column<short>(type: "smallint", nullable: true),
                display_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_choice_categories", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "config_values",
            schema: "public",
            columns: table => new
            {
                code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                value = table.Column<string>(type: "text", nullable: true),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_by_staff_account_id = table.Column<int>(type: "integer", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_config_values", x => x.code);
            });

        migrationBuilder.CreateTable(
            name: "counters",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                display_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_counters", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "denominations",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                face_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                display_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_denominations", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "discount_policies",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                discount_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                is_auto_apply = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                days_of_week = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_discount_policies", x => x.id);
                table.CheckConstraint("ck_discount_policy_discount_type", "discount_type IN ('TICKET_THRESHOLD', 'QUANTITY_ITEM')");
            });

        migrationBuilder.CreateTable(
            name: "domain_versions",
            schema: "public",
            columns: table => new
            {
                scope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_by_source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_domain_versions", x => x.scope);
            });

        migrationBuilder.CreateTable(
            name: "kitchen_stations",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                display_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_kitchen_stations", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "outbox_message_consumers",
            schema: "public",
            columns: table => new
            {
                outbox_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_outbox_message_consumers", x => new { x.outbox_message_id, x.name });
            });

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: "public",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<string>(type: "text", nullable: false),
                content = table.Column<string>(type: "jsonb", maxLength: 2000, nullable: false),
                occurred_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                processed_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                error = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_outbox_messages", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "payment_methods",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                display_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_payment_methods", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "permission_groups",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                display_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_permission_groups", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "price_tables",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                begin_date = table.Column<DateOnly>(type: "date", nullable: true),
                end_date = table.Column<DateOnly>(type: "date", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_price_tables", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "rag_document_chunks",
            schema: "public",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                source_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                source_ref = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                chunk_text = table.Column<string>(type: "text", nullable: false),
                chunk_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                embedding = table.Column<Vector>(type: "vector(1536)", nullable: true),
                embedding_model = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                token_count = table.Column<int>(type: "integer", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                indexed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_rag_document_chunks", x => x.id);
                table.CheckConstraint("ck_rag_document_chunk_source_type", "source_type IN ('GLOSSARY', 'BUSINESS_RULE', 'PROCESS_PLAYBOOK', 'AI_SPEC')");
            });

        migrationBuilder.CreateTable(
            name: "roles",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                is_system_role = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_roles", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "shifts",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                begin_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                is_next_day = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_shifts", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "uoms",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_uoms", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "areas",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                counter_id = table.Column<int>(type: "integer", nullable: false),
                name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                display_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_areas", x => x.id);
                table.ForeignKey(
                    name: "fk_areas_counters_counter_id",
                    column: x => x.counter_id,
                    principalSchema: "public",
                    principalTable: "counters",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "printers",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                kitchen_station_id = table.Column<int>(type: "integer", nullable: true),
                counter_id = table.Column<int>(type: "integer", nullable: true),
                ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                printer_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                print_copy = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_printers", x => x.id);
                table.CheckConstraint("ck_printer_type", "type IN ('KITCHEN', 'CASHIER')");
                table.ForeignKey(
                    name: "fk_printers_counters_counter_id",
                    column: x => x.counter_id,
                    principalSchema: "public",
                    principalTable: "counters",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_printers_kitchen_stations_kitchen_station_id",
                    column: x => x.kitchen_station_id,
                    principalSchema: "public",
                    principalTable: "kitchen_stations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "permissions",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                permission_group_id = table.Column<int>(type: "integer", nullable: false),
                display_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_permissions", x => x.id);
                table.ForeignKey(
                    name: "fk_permissions_permission_groups_permission_group_id",
                    column: x => x.permission_group_id,
                    principalSchema: "public",
                    principalTable: "permission_groups",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "price_variants",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                price_table_id = table.Column<int>(type: "integer", nullable: false),
                code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                begin_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                day_mask = table.Column<int>(type: "integer", nullable: true),
                applies_to_all_areas = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_price_variants", x => x.id);
                table.ForeignKey(
                    name: "fk_price_variants_price_tables_price_table_id",
                    column: x => x.price_table_id,
                    principalSchema: "public",
                    principalTable: "price_tables",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "staff_accounts",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                role_id = table.Column<int>(type: "integer", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                is_locked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_staff_accounts", x => x.id);
                table.ForeignKey(
                    name: "fk_staff_accounts_roles_role_id",
                    column: x => x.role_id,
                    principalSchema: "public",
                    principalTable: "roles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "items",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                base_uom_id = table.Column<int>(type: "integer", nullable: false),
                vat_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                is_stockable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                has_recipe = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                low_stock_threshold = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                kitchen_station_id = table.Column<int>(type: "integer", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_items", x => x.id);
                table.ForeignKey(
                    name: "fk_items_kitchen_stations_kitchen_station_id",
                    column: x => x.kitchen_station_id,
                    principalSchema: "public",
                    principalTable: "kitchen_stations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "fk_items_uoms_base_uom_id",
                    column: x => x.base_uom_id,
                    principalSchema: "public",
                    principalTable: "uoms",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "area_menu_categories",
            schema: "public",
            columns: table => new
            {
                area_id = table.Column<int>(type: "integer", nullable: false),
                category_id = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_area_menu_categories", x => new { x.area_id, x.category_id });
                table.ForeignKey(
                    name: "fk_area_menu_categories_areas_area_id",
                    column: x => x.area_id,
                    principalSchema: "public",
                    principalTable: "areas",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_area_menu_categories_categories_category_id",
                    column: x => x.category_id,
                    principalSchema: "public",
                    principalTable: "categories",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "tables",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                area_id = table.Column<int>(type: "integer", nullable: false),
                code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                seat_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "AVAILABLE"),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_tables", x => x.id);
                table.CheckConstraint("ck_table_status", "status IN ('AVAILABLE', 'OCCUPIED')");
                table.ForeignKey(
                    name: "fk_tables_areas_area_id",
                    column: x => x.area_id,
                    principalSchema: "public",
                    principalTable: "areas",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "price_variant_areas",
            schema: "public",
            columns: table => new
            {
                price_variant_id = table.Column<int>(type: "integer", nullable: false),
                area_id = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_price_variant_areas", x => new { x.price_variant_id, x.area_id });
                table.ForeignKey(
                    name: "fk_price_variant_areas_areas_area_id",
                    column: x => x.area_id,
                    principalSchema: "public",
                    principalTable: "areas",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_price_variant_areas_price_variants_price_variant_id",
                    column: x => x.price_variant_id,
                    principalSchema: "public",
                    principalTable: "price_variants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ai_conversations",
            schema: "public",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                owner_staff_id = table.Column<int>(type: "integer", nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                ended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                message_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_ai_conversations", x => x.id);
                table.CheckConstraint("ck_ai_conversation_status", "status IN ('ACTIVE', 'ENDED')");
                table.ForeignKey(
                    name: "fk_ai_conversations_staff_accounts_owner_staff_id",
                    column: x => x.owner_staff_id,
                    principalSchema: "public",
                    principalTable: "staff_accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ai_notifications",
            schema: "public",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                recipient_staff_id = table.Column<int>(type: "integer", nullable: false),
                type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                body = table.Column<string>(type: "text", nullable: false),
                ref_item_id = table.Column<int>(type: "integer", nullable: true),
                ref_counter_id = table.Column<int>(type: "integer", nullable: true),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "UNREAD"),
                read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                dismissed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                triggered_by_cron_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_ai_notifications", x => x.id);
                table.CheckConstraint("ck_ai_notification_status", "status IN ('UNREAD', 'READ', 'DISMISSED', 'ARCHIVED')");
                table.CheckConstraint("ck_ai_notification_type", "type IN ('LOW_STOCK', 'EOD_SUMMARY')");
                table.ForeignKey(
                    name: "fk_ai_notifications_staff_accounts_recipient_staff_id",
                    column: x => x.recipient_staff_id,
                    principalSchema: "public",
                    principalTable: "staff_accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "cash_drawer_sessions",
            schema: "public",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                counter_id = table.Column<int>(type: "integer", nullable: false),
                opened_by_staff_account_id = table.Column<int>(type: "integer", nullable: false),
                opened_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                opening_cash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                closed_by_staff_account_id = table.Column<int>(type: "integer", nullable: true),
                closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                expected_closing_cash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                actual_closing_cash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                variance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "OPEN"),
                notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                version = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_cash_drawer_sessions", x => x.id);
                table.CheckConstraint("ck_cash_drawer_session_status", "status IN ('OPEN', 'CLOSED')");
                table.ForeignKey(
                    name: "fk_cash_drawer_sessions_counters_counter_id",
                    column: x => x.counter_id,
                    principalSchema: "public",
                    principalTable: "counters",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_cash_drawer_sessions_staff_accounts_closed_by_staff_account",
                    column: x => x.closed_by_staff_account_id,
                    principalSchema: "public",
                    principalTable: "staff_accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_cash_drawer_sessions_staff_accounts_opened_by_staff_account",
                    column: x => x.opened_by_staff_account_id,
                    principalSchema: "public",
                    principalTable: "staff_accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "staff_account_permissions",
            schema: "public",
            columns: table => new
            {
                staff_account_id = table.Column<int>(type: "integer", nullable: false),
                permission_id = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_staff_account_permissions", x => new { x.staff_account_id, x.permission_id });
                table.ForeignKey(
                    name: "fk_staff_account_permissions_permissions_permission_id",
                    column: x => x.permission_id,
                    principalSchema: "public",
                    principalTable: "permissions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_staff_account_permissions_staff_accounts_staff_account_id",
                    column: x => x.staff_account_id,
                    principalSchema: "public",
                    principalTable: "staff_accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "bom_lines",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                sellable_item_id = table.Column<int>(type: "integer", nullable: false),
                material_item_id = table.Column<int>(type: "integer", nullable: false),
                quantity = table.Column<decimal>(type: "numeric(18,5)", precision: 18, scale: 5, nullable: false),
                uom_id = table.Column<int>(type: "integer", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_bom_lines", x => x.id);
                table.ForeignKey(
                    name: "fk_bom_lines_items_material_item_id",
                    column: x => x.material_item_id,
                    principalSchema: "public",
                    principalTable: "items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_bom_lines_items_sellable_item_id",
                    column: x => x.sellable_item_id,
                    principalSchema: "public",
                    principalTable: "items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_bom_lines_uoms_uom_id",
                    column: x => x.uom_id,
                    principalSchema: "public",
                    principalTable: "uoms",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "discount_policy_conditions",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                discount_policy_id = table.Column<int>(type: "integer", nullable: false),
                threshold_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                item_id = table.Column<int>(type: "integer", nullable: true),
                quantity_threshold = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                area_id = table.Column<int>(type: "integer", nullable: true),
                apply_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                discount_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                display_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_discount_policy_conditions", x => x.id);
                table.CheckConstraint("ck_discount_policy_condition_apply_type", "apply_type IN ('PERCENT', 'FIXED')");
                table.ForeignKey(
                    name: "fk_discount_policy_conditions_areas_area_id",
                    column: x => x.area_id,
                    principalSchema: "public",
                    principalTable: "areas",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_discount_policy_conditions_discount_policies_discount_polic",
                    column: x => x.discount_policy_id,
                    principalSchema: "public",
                    principalTable: "discount_policies",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_discount_policy_conditions_items_item_id",
                    column: x => x.item_id,
                    principalSchema: "public",
                    principalTable: "items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "item_categories",
            schema: "public",
            columns: table => new
            {
                item_id = table.Column<int>(type: "integer", nullable: false),
                category_id = table.Column<int>(type: "integer", nullable: false),
                is_main = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_item_categories", x => new { x.item_id, x.category_id });
                table.ForeignKey(
                    name: "fk_item_categories_categories_category_id",
                    column: x => x.category_id,
                    principalSchema: "public",
                    principalTable: "categories",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_item_categories_items_item_id",
                    column: x => x.item_id,
                    principalSchema: "public",
                    principalTable: "items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "item_stocks",
            schema: "public",
            columns: table => new
            {
                item_id = table.Column<int>(type: "integer", nullable: false),
                current_qty = table.Column<decimal>(type: "numeric(22,5)", precision: 22, scale: 5, nullable: false, defaultValue: 0m),
                reserved_qty = table.Column<decimal>(type: "numeric(22,5)", precision: 22, scale: 5, nullable: false, defaultValue: 0m),
                last_movement_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_item_stocks", x => x.item_id);
                table.ForeignKey(
                    name: "fk_item_stocks_items_item_id",
                    column: x => x.item_id,
                    principalSchema: "public",
                    principalTable: "items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "item_uom_conversions",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                item_id = table.Column<int>(type: "integer", nullable: false),
                uom_id = table.Column<int>(type: "integer", nullable: false),
                factor_to_base = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_item_uom_conversions", x => x.id);
                table.ForeignKey(
                    name: "fk_item_uom_conversions_items_item_id",
                    column: x => x.item_id,
                    principalSchema: "public",
                    principalTable: "items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_item_uom_conversions_uoms_uom_id",
                    column: x => x.uom_id,
                    principalSchema: "public",
                    principalTable: "uoms",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "modifiers",
            schema: "public",
            columns: table => new
            {
                choice_category_id = table.Column<int>(type: "integer", nullable: false),
                item_id = table.Column<int>(type: "integer", nullable: false),
                extra_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                min_per_modifier = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                max_per_modifier = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                display_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_modifiers", x => new { x.choice_category_id, x.item_id });
                table.ForeignKey(
                    name: "fk_modifiers_choice_categories_choice_category_id",
                    column: x => x.choice_category_id,
                    principalSchema: "public",
                    principalTable: "choice_categories",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_modifiers_items_item_id",
                    column: x => x.item_id,
                    principalSchema: "public",
                    principalTable: "items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "price_entries",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                price_variant_id = table.Column<int>(type: "integer", nullable: false),
                item_id = table.Column<int>(type: "integer", nullable: false),
                price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                is_vat_included = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_price_entries", x => x.id);
                table.ForeignKey(
                    name: "fk_price_entries_items_item_id",
                    column: x => x.item_id,
                    principalSchema: "public",
                    principalTable: "items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_price_entries_price_variants_price_variant_id",
                    column: x => x.price_variant_id,
                    principalSchema: "public",
                    principalTable: "price_variants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "set_menus",
            schema: "public",
            columns: table => new
            {
                item_id = table.Column<int>(type: "integer", nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_set_menus", x => x.item_id);
                table.ForeignKey(
                    name: "fk_set_menus_items_item_id",
                    column: x => x.item_id,
                    principalSchema: "public",
                    principalTable: "items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "stock_movements",
            schema: "public",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                item_id = table.Column<int>(type: "integer", nullable: false),
                movement_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                qty_in_base = table.Column<decimal>(type: "numeric(22,5)", precision: 22, scale: 5, nullable: false),
                balance_after = table.Column<decimal>(type: "numeric(22,5)", precision: 22, scale: 5, nullable: false),
                reference_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                reference_id = table.Column<long>(type: "bigint", nullable: true),
                reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_by_staff_id = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_stock_movements", x => x.id);
                table.CheckConstraint("ck_stock_movement_reference_type", "reference_type IS NULL OR reference_type IN ('ORDER_DISH', 'MANUAL')");
                table.CheckConstraint("ck_stock_movement_type", "movement_type IN ('STOCK_IN', 'ADJUST_IN', 'ADJUST_OUT', 'DEDUCT')");
                table.ForeignKey(
                    name: "fk_stock_movements_items_item_id",
                    column: x => x.item_id,
                    principalSchema: "public",
                    principalTable: "items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_stock_movements_staff_accounts_created_by_staff_id",
                    column: x => x.created_by_staff_id,
                    principalSchema: "public",
                    principalTable: "staff_accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ai_messages",
            schema: "public",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                conversation_id = table.Column<long>(type: "bigint", nullable: false),
                role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                content = table.Column<string>(type: "text", nullable: false),
                sequence_number = table.Column<int>(type: "integer", nullable: false),
                token_count = table.Column<int>(type: "integer", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_ai_messages", x => x.id);
                table.CheckConstraint("ck_ai_message_role", "role IN ('USER', 'ASSISTANT', 'TOOL_CALL', 'SYSTEM')");
                table.ForeignKey(
                    name: "fk_ai_messages_ai_conversations_conversation_id",
                    column: x => x.conversation_id,
                    principalSchema: "public",
                    principalTable: "ai_conversations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "cash_drawer_cash_counts",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                cash_drawer_session_id = table.Column<long>(type: "bigint", nullable: false),
                denomination_id = table.Column<int>(type: "integer", nullable: false),
                phase = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_cash_drawer_cash_counts", x => x.id);
                table.CheckConstraint("ck_cash_drawer_cash_count_phase", "phase IN ('OPENING', 'CLOSING')");
                table.ForeignKey(
                    name: "fk_cash_drawer_cash_counts_cash_drawer_sessions_cash_drawer_se",
                    column: x => x.cash_drawer_session_id,
                    principalSchema: "public",
                    principalTable: "cash_drawer_sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_cash_drawer_cash_counts_denominations_denomination_id",
                    column: x => x.denomination_id,
                    principalSchema: "public",
                    principalTable: "denominations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "tickets",
            schema: "public",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                table_id = table.Column<int>(type: "integer", nullable: false),
                area_id = table.Column<int>(type: "integer", nullable: false),
                counter_id = table.Column<int>(type: "integer", nullable: false),
                cash_drawer_session_id = table.Column<long>(type: "bigint", nullable: false),
                shift_id = table.Column<int>(type: "integer", nullable: false),
                guest_count = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                waiter_staff_id = table.Column<int>(type: "integer", nullable: true),
                manager_staff_id = table.Column<int>(type: "integer", nullable: true),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "OPEN"),
                opened_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                cancellation_reason_id = table.Column<int>(type: "integer", nullable: true),
                cancellation_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                discount_policy_id = table.Column<int>(type: "integer", nullable: true),
                discount_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                service_charge_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                service_charge_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                vat_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                vat_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                paid_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                change_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                guest_qr_token = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                guest_qr_generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                version = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_tickets", x => x.id);
                table.CheckConstraint("ck_ticket_status", "status IN ('OPEN', 'CLOSED', 'CANCELLED')");
                table.ForeignKey(
                    name: "fk_tickets_areas_area_id",
                    column: x => x.area_id,
                    principalSchema: "public",
                    principalTable: "areas",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_tickets_cancellation_reasons_cancellation_reason_id",
                    column: x => x.cancellation_reason_id,
                    principalSchema: "public",
                    principalTable: "cancellation_reasons",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_tickets_cash_drawer_sessions_cash_drawer_session_id",
                    column: x => x.cash_drawer_session_id,
                    principalSchema: "public",
                    principalTable: "cash_drawer_sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_tickets_counters_counter_id",
                    column: x => x.counter_id,
                    principalSchema: "public",
                    principalTable: "counters",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_tickets_discount_policies_discount_policy_id",
                    column: x => x.discount_policy_id,
                    principalSchema: "public",
                    principalTable: "discount_policies",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_tickets_shifts_shift_id",
                    column: x => x.shift_id,
                    principalSchema: "public",
                    principalTable: "shifts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_tickets_staff_accounts_manager_staff_id",
                    column: x => x.manager_staff_id,
                    principalSchema: "public",
                    principalTable: "staff_accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_tickets_staff_accounts_waiter_staff_id",
                    column: x => x.waiter_staff_id,
                    principalSchema: "public",
                    principalTable: "staff_accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_tickets_tables_table_id",
                    column: x => x.table_id,
                    principalSchema: "public",
                    principalTable: "tables",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "set_menu_details",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                set_menu_item_id = table.Column<int>(type: "integer", nullable: false),
                detail_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                component_item_id = table.Column<int>(type: "integer", nullable: true),
                choice_category_id = table.Column<int>(type: "integer", nullable: true),
                quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                is_fixed = table.Column<bool>(type: "boolean", nullable: true),
                display_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_set_menu_details", x => x.id);
                table.CheckConstraint("ck_set_menu_detail_type", "detail_type IN ('COMPONENT', 'CHOICE_CATEGORY')");
                table.ForeignKey(
                    name: "fk_set_menu_details_choice_categories_choice_category_id",
                    column: x => x.choice_category_id,
                    principalSchema: "public",
                    principalTable: "choice_categories",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_set_menu_details_items_component_item_id",
                    column: x => x.component_item_id,
                    principalSchema: "public",
                    principalTable: "items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_set_menu_details_set_menus_set_menu_item_id",
                    column: x => x.set_menu_item_id,
                    principalSchema: "public",
                    principalTable: "set_menus",
                    principalColumn: "item_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ai_tool_call_logs",
            schema: "public",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                message_id = table.Column<long>(type: "bigint", nullable: false),
                tool_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                input_json = table.Column<string>(type: "jsonb", nullable: false),
                output_json = table.Column<string>(type: "jsonb", nullable: true),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "SUCCESS"),
                error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                latency_ms = table.Column<int>(type: "integer", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_ai_tool_call_logs", x => x.id);
                table.CheckConstraint("ck_ai_tool_call_log_status", "status IN ('SUCCESS', 'ERROR', 'TIMEOUT', 'REJECTED_PERMISSION')");
                table.ForeignKey(
                    name: "fk_ai_tool_call_logs_ai_messages_message_id",
                    column: x => x.message_id,
                    principalSchema: "public",
                    principalTable: "ai_messages",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "e_invoices",
            schema: "public",
            columns: table => new
            {
                ticket_id = table.Column<long>(type: "bigint", nullable: false),
                customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                tax_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                external_invoice_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_e_invoices", x => x.ticket_id);
                table.ForeignKey(
                    name: "fk_e_invoices_tickets_ticket_id",
                    column: x => x.ticket_id,
                    principalSchema: "public",
                    principalTable: "tickets",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "orders",
            schema: "public",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ticket_id = table.Column<long>(type: "bigint", nullable: false),
                order_number = table.Column<short>(type: "smallint", nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "DRAFT"),
                sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by_staff_id = table.Column<int>(type: "integer", nullable: true),
                notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                version = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_orders", x => x.id);
                table.CheckConstraint("ck_order_status", "status IN ('DRAFT', 'SENT', 'PROCESSING', 'DONE', 'DELETED')");
                table.ForeignKey(
                    name: "fk_orders_staff_accounts_created_by_staff_id",
                    column: x => x.created_by_staff_id,
                    principalSchema: "public",
                    principalTable: "staff_accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_orders_tickets_ticket_id",
                    column: x => x.ticket_id,
                    principalSchema: "public",
                    principalTable: "tickets",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "reservations",
            schema: "public",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                table_id = table.Column<int>(type: "integer", nullable: false),
                customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                customer_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                guest_count = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                target_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "BOOKED"),
                arrived_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                cancellation_reason_id = table.Column<int>(type: "integer", nullable: true),
                cancellation_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                linked_ticket_id = table.Column<long>(type: "bigint", nullable: true),
                created_by_staff_id = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_reservations", x => x.id);
                table.CheckConstraint("ck_reservation_status", "status IN ('BOOKED', 'ARRIVED', 'CANCELLED')");
                table.ForeignKey(
                    name: "fk_reservations_cancellation_reasons_cancellation_reason_id",
                    column: x => x.cancellation_reason_id,
                    principalSchema: "public",
                    principalTable: "cancellation_reasons",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_reservations_staff_accounts_created_by_staff_id",
                    column: x => x.created_by_staff_id,
                    principalSchema: "public",
                    principalTable: "staff_accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_reservations_tables_table_id",
                    column: x => x.table_id,
                    principalSchema: "public",
                    principalTable: "tables",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_reservations_tickets_linked_ticket_id",
                    column: x => x.linked_ticket_id,
                    principalSchema: "public",
                    principalTable: "tickets",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ticket_item_sums",
            schema: "public",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ticket_id = table.Column<long>(type: "bigint", nullable: false),
                item_id = table.Column<int>(type: "integer", nullable: false),
                item_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                item_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                uom_id = table.Column<int>(type: "integer", nullable: false),
                uom_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                uom_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                discount_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                choice_price_per_unit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                vat_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                service_charge_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                total_quantity = table.Column<decimal>(type: "numeric(22,3)", precision: 22, scale: 3, nullable: false, defaultValue: 0m),
                total_choice_amount = table.Column<decimal>(type: "numeric(22,2)", precision: 22, scale: 2, nullable: false, defaultValue: 0m),
                subtotal = table.Column<decimal>(type: "numeric(22,2)", precision: 22, scale: 2, nullable: false, defaultValue: 0m),
                total_discount = table.Column<decimal>(type: "numeric(22,2)", precision: 22, scale: 2, nullable: false, defaultValue: 0m),
                total_vat = table.Column<decimal>(type: "numeric(22,2)", precision: 22, scale: 2, nullable: false, defaultValue: 0m),
                total_amount = table.Column<decimal>(type: "numeric(22,2)", precision: 22, scale: 2, nullable: false, defaultValue: 0m),
                max_order_item_id = table.Column<long>(type: "bigint", nullable: false),
                display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_ticket_item_sums", x => x.id);
                table.ForeignKey(
                    name: "fk_ticket_item_sums_items_item_id",
                    column: x => x.item_id,
                    principalSchema: "public",
                    principalTable: "items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_ticket_item_sums_tickets_ticket_id",
                    column: x => x.ticket_id,
                    principalSchema: "public",
                    principalTable: "tickets",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_ticket_item_sums_uoms_uom_id",
                    column: x => x.uom_id,
                    principalSchema: "public",
                    principalTable: "uoms",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ticket_payment_details",
            schema: "public",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ticket_id = table.Column<long>(type: "bigint", nullable: false),
                payment_method_id = table.Column<int>(type: "integer", nullable: false),
                amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "PENDING"),
                processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                processed_by_staff_id = table.Column<int>(type: "integer", nullable: false),
                transaction_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_ticket_payment_details", x => x.id);
                table.CheckConstraint("ck_ticket_payment_detail_status", "status IN ('PENDING', 'SUCCESS', 'CANCELLED', 'DELETED')");
                table.ForeignKey(
                    name: "fk_ticket_payment_details_payment_methods_payment_method_id",
                    column: x => x.payment_method_id,
                    principalSchema: "public",
                    principalTable: "payment_methods",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_ticket_payment_details_staff_accounts_processed_by_staff_id",
                    column: x => x.processed_by_staff_id,
                    principalSchema: "public",
                    principalTable: "staff_accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_ticket_payment_details_tickets_ticket_id",
                    column: x => x.ticket_id,
                    principalSchema: "public",
                    principalTable: "tickets",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "cart_items",
            schema: "public",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                order_id = table.Column<long>(type: "bigint", nullable: false),
                item_id = table.Column<int>(type: "integer", nullable: false),
                item_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                item_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                uom_id = table.Column<int>(type: "integer", nullable: false),
                uom_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                uom_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false, defaultValue: 1m),
                unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                line_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_cart_items", x => x.id);
                table.ForeignKey(
                    name: "fk_cart_items_items_item_id",
                    column: x => x.item_id,
                    principalSchema: "public",
                    principalTable: "items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_cart_items_orders_order_id",
                    column: x => x.order_id,
                    principalSchema: "public",
                    principalTable: "orders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_cart_items_uoms_uom_id",
                    column: x => x.uom_id,
                    principalSchema: "public",
                    principalTable: "uoms",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "order_items",
            schema: "public",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                order_id = table.Column<long>(type: "bigint", nullable: false),
                ticket_id = table.Column<long>(type: "bigint", nullable: false),
                item_id = table.Column<int>(type: "integer", nullable: false),
                item_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                item_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                uom_id = table.Column<int>(type: "integer", nullable: false),
                uom_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                uom_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false, defaultValue: 1m),
                unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                line_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                kitchen_station_id = table.Column<int>(type: "integer", nullable: true),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "PENDING"),
                sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                start_cook_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ready_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                done_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                cancellation_reason_id = table.Column<int>(type: "integer", nullable: true),
                cancellation_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                cancelled_by_staff_id = table.Column<int>(type: "integer", nullable: true),
                original_order_item_id = table.Column<long>(type: "bigint", nullable: true),
                notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                version = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_order_items", x => x.id);
                table.CheckConstraint("ck_order_item_status", "status IN ('PENDING', 'PROCESSING', 'READY', 'DONE', 'CANCELLED')");
                table.ForeignKey(
                    name: "fk_order_items_cancellation_reasons_cancellation_reason_id",
                    column: x => x.cancellation_reason_id,
                    principalSchema: "public",
                    principalTable: "cancellation_reasons",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_order_items_items_item_id",
                    column: x => x.item_id,
                    principalSchema: "public",
                    principalTable: "items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_order_items_kitchen_stations_kitchen_station_id",
                    column: x => x.kitchen_station_id,
                    principalSchema: "public",
                    principalTable: "kitchen_stations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "fk_order_items_order_items_original_order_item_id",
                    column: x => x.original_order_item_id,
                    principalSchema: "public",
                    principalTable: "order_items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_order_items_orders_order_id",
                    column: x => x.order_id,
                    principalSchema: "public",
                    principalTable: "orders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_order_items_staff_accounts_cancelled_by_staff_id",
                    column: x => x.cancelled_by_staff_id,
                    principalSchema: "public",
                    principalTable: "staff_accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_order_items_tickets_ticket_id",
                    column: x => x.ticket_id,
                    principalSchema: "public",
                    principalTable: "tickets",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_order_items_uoms_uom_id",
                    column: x => x.uom_id,
                    principalSchema: "public",
                    principalTable: "uoms",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "cart_item_details",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                cart_item_id = table.Column<long>(type: "bigint", nullable: false),
                choice_category_id = table.Column<int>(type: "integer", nullable: true),
                item_id = table.Column<int>(type: "integer", nullable: false),
                item_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                component_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false, defaultValue: 1m),
                extra_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_cart_item_details", x => x.id);
                table.CheckConstraint("ck_cart_item_detail_component_type", "component_type IN ('MAIN_COMPONENT', 'MODIFIER')");
                table.ForeignKey(
                    name: "fk_cart_item_details_cart_items_cart_item_id",
                    column: x => x.cart_item_id,
                    principalSchema: "public",
                    principalTable: "cart_items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_cart_item_details_choice_categories_choice_category_id",
                    column: x => x.choice_category_id,
                    principalSchema: "public",
                    principalTable: "choice_categories",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_cart_item_details_items_item_id",
                    column: x => x.item_id,
                    principalSchema: "public",
                    principalTable: "items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "order_item_details",
            schema: "public",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                order_item_id = table.Column<long>(type: "bigint", nullable: false),
                choice_category_id = table.Column<int>(type: "integer", nullable: true),
                item_id = table.Column<int>(type: "integer", nullable: false),
                item_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                component_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false, defaultValue: 1m),
                extra_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_order_item_details", x => x.id);
                table.CheckConstraint("ck_order_item_detail_component_type", "component_type IN ('MAIN_COMPONENT', 'MODIFIER')");
                table.ForeignKey(
                    name: "fk_order_item_details_choice_categories_choice_category_id",
                    column: x => x.choice_category_id,
                    principalSchema: "public",
                    principalTable: "choice_categories",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_order_item_details_items_item_id",
                    column: x => x.item_id,
                    principalSchema: "public",
                    principalTable: "items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_order_item_details_order_items_order_item_id",
                    column: x => x.order_item_id,
                    principalSchema: "public",
                    principalTable: "order_items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_ai_conversation_owner_updated",
            schema: "public",
            table: "ai_conversations",
            columns: new[] { "owner_staff_id", "updated_at" });

        migrationBuilder.CreateIndex(
            name: "ix_ai_conversations_owner_staff_id",
            schema: "public",
            table: "ai_conversations",
            column: "owner_staff_id");

        migrationBuilder.CreateIndex(
            name: "ix_ai_conversations_status",
            schema: "public",
            table: "ai_conversations",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_ai_messages_conversation_id",
            schema: "public",
            table: "ai_messages",
            column: "conversation_id");

        migrationBuilder.CreateIndex(
            name: "ux_ai_message_conv_seq",
            schema: "public",
            table: "ai_messages",
            columns: new[] { "conversation_id", "sequence_number" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_ai_notification_recipient_status",
            schema: "public",
            table: "ai_notifications",
            columns: new[] { "recipient_staff_id", "status", "updated_at" });

        migrationBuilder.CreateIndex(
            name: "ix_ai_notification_ref_item",
            schema: "public",
            table: "ai_notifications",
            column: "ref_item_id");

        migrationBuilder.CreateIndex(
            name: "ix_ai_notification_type_time",
            schema: "public",
            table: "ai_notifications",
            columns: new[] { "type", "created_at" });

        migrationBuilder.CreateIndex(
            name: "ix_ai_tool_call_log_status",
            schema: "public",
            table: "ai_tool_call_logs",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_ai_tool_call_log_tool_time",
            schema: "public",
            table: "ai_tool_call_logs",
            columns: new[] { "tool_name", "created_at" });

        migrationBuilder.CreateIndex(
            name: "ix_ai_tool_call_logs_message_id",
            schema: "public",
            table: "ai_tool_call_logs",
            column: "message_id");

        migrationBuilder.CreateIndex(
            name: "ix_area_menu_category_category",
            schema: "public",
            table: "area_menu_categories",
            column: "category_id");

        migrationBuilder.CreateIndex(
            name: "ix_area_counter_active",
            schema: "public",
            table: "areas",
            columns: new[] { "counter_id", "is_active" });

        migrationBuilder.CreateIndex(
            name: "ix_areas_counter_id",
            schema: "public",
            table: "areas",
            column: "counter_id");

        migrationBuilder.CreateIndex(
            name: "ix_audit_log_actor_time",
            schema: "public",
            table: "audit_logs",
            columns: new[] { "actor_staff_account_id", "timestamp" });

        migrationBuilder.CreateIndex(
            name: "ix_audit_log_entity_time",
            schema: "public",
            table: "audit_logs",
            columns: new[] { "entity_type", "entity_id", "timestamp" });

        migrationBuilder.CreateIndex(
            name: "ix_audit_log_time",
            schema: "public",
            table: "audit_logs",
            column: "timestamp");

        migrationBuilder.CreateIndex(
            name: "ix_bom_lines_material_item_id",
            schema: "public",
            table: "bom_lines",
            column: "material_item_id");

        migrationBuilder.CreateIndex(
            name: "ix_bom_lines_sellable_item_id",
            schema: "public",
            table: "bom_lines",
            column: "sellable_item_id");

        migrationBuilder.CreateIndex(
            name: "ix_bom_lines_uom_id",
            schema: "public",
            table: "bom_lines",
            column: "uom_id");

        migrationBuilder.CreateIndex(
            name: "ux_bom_line_recipe",
            schema: "public",
            table: "bom_lines",
            columns: new[] { "sellable_item_id", "material_item_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_cancellation_reasons_code",
            schema: "public",
            table: "cancellation_reasons",
            column: "code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_cart_item_detail_item_type",
            schema: "public",
            table: "cart_item_details",
            columns: new[] { "cart_item_id", "component_type" });

        migrationBuilder.CreateIndex(
            name: "ix_cart_item_details_cart_item_id",
            schema: "public",
            table: "cart_item_details",
            column: "cart_item_id");

        migrationBuilder.CreateIndex(
            name: "ix_cart_item_details_choice_category_id",
            schema: "public",
            table: "cart_item_details",
            column: "choice_category_id");

        migrationBuilder.CreateIndex(
            name: "ix_cart_item_details_item_id",
            schema: "public",
            table: "cart_item_details",
            column: "item_id");

        migrationBuilder.CreateIndex(
            name: "ix_cart_items_item_id",
            schema: "public",
            table: "cart_items",
            column: "item_id");

        migrationBuilder.CreateIndex(
            name: "ix_cart_items_order_id",
            schema: "public",
            table: "cart_items",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "ix_cart_items_uom_id",
            schema: "public",
            table: "cart_items",
            column: "uom_id");

        migrationBuilder.CreateIndex(
            name: "ix_cash_drawer_cash_count_session_phase",
            schema: "public",
            table: "cash_drawer_cash_counts",
            columns: new[] { "cash_drawer_session_id", "phase" });

        migrationBuilder.CreateIndex(
            name: "ix_cash_drawer_cash_counts_denomination_id",
            schema: "public",
            table: "cash_drawer_cash_counts",
            column: "denomination_id");

        migrationBuilder.CreateIndex(
            name: "ux_cash_drawer_cash_count",
            schema: "public",
            table: "cash_drawer_cash_counts",
            columns: new[] { "cash_drawer_session_id", "phase", "denomination_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_cash_drawer_session_counter_status",
            schema: "public",
            table: "cash_drawer_sessions",
            columns: new[] { "counter_id", "status" });

        migrationBuilder.CreateIndex(
            name: "ix_cash_drawer_sessions_closed_by_staff_account_id",
            schema: "public",
            table: "cash_drawer_sessions",
            column: "closed_by_staff_account_id");

        migrationBuilder.CreateIndex(
            name: "ix_cash_drawer_sessions_opened_at",
            schema: "public",
            table: "cash_drawer_sessions",
            column: "opened_at");

        migrationBuilder.CreateIndex(
            name: "ix_cash_drawer_sessions_opened_by_staff_account_id",
            schema: "public",
            table: "cash_drawer_sessions",
            column: "opened_by_staff_account_id");

        migrationBuilder.CreateIndex(
            name: "ux_cash_drawer_session_counter_open",
            schema: "public",
            table: "cash_drawer_sessions",
            column: "counter_id",
            unique: true,
            filter: "status = 'OPEN'");

        migrationBuilder.CreateIndex(
            name: "ix_categories_code",
            schema: "public",
            table: "categories",
            column: "code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_categories_parent_id",
            schema: "public",
            table: "categories",
            column: "parent_id");

        migrationBuilder.CreateIndex(
            name: "ix_category_path",
            schema: "public",
            table: "categories",
            column: "path");

        migrationBuilder.CreateIndex(
            name: "ix_choice_categories_name",
            schema: "public",
            table: "choice_categories",
            column: "name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_counters_name",
            schema: "public",
            table: "counters",
            column: "name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_denominations_face_value",
            schema: "public",
            table: "denominations",
            column: "face_value",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_discount_policies_code",
            schema: "public",
            table: "discount_policies",
            column: "code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_discount_policy_condition_area",
            schema: "public",
            table: "discount_policy_conditions",
            column: "area_id");

        migrationBuilder.CreateIndex(
            name: "ix_discount_policy_condition_item",
            schema: "public",
            table: "discount_policy_conditions",
            column: "item_id");

        migrationBuilder.CreateIndex(
            name: "ix_discount_policy_conditions_discount_policy_id",
            schema: "public",
            table: "discount_policy_conditions",
            column: "discount_policy_id");

        migrationBuilder.CreateIndex(
            name: "ix_item_category_category",
            schema: "public",
            table: "item_categories",
            column: "category_id");

        migrationBuilder.CreateIndex(
            name: "ix_item_category_item_main",
            schema: "public",
            table: "item_categories",
            columns: new[] { "item_id", "is_main" });

        migrationBuilder.CreateIndex(
            name: "ix_item_stock_qty",
            schema: "public",
            table: "item_stocks",
            column: "current_qty");

        migrationBuilder.CreateIndex(
            name: "ix_item_stock_updated",
            schema: "public",
            table: "item_stocks",
            column: "updated_at");

        migrationBuilder.CreateIndex(
            name: "ix_item_uom_conversions_item_id",
            schema: "public",
            table: "item_uom_conversions",
            column: "item_id");

        migrationBuilder.CreateIndex(
            name: "ix_item_uom_conversions_uom_id",
            schema: "public",
            table: "item_uom_conversions",
            column: "uom_id");

        migrationBuilder.CreateIndex(
            name: "ux_item_uom_conversion",
            schema: "public",
            table: "item_uom_conversions",
            columns: new[] { "item_id", "uom_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_item_kitchen_station",
            schema: "public",
            table: "items",
            column: "kitchen_station_id");

        migrationBuilder.CreateIndex(
            name: "ix_item_stockable",
            schema: "public",
            table: "items",
            column: "is_stockable");

        migrationBuilder.CreateIndex(
            name: "ix_items_base_uom_id",
            schema: "public",
            table: "items",
            column: "base_uom_id");

        migrationBuilder.CreateIndex(
            name: "ix_items_code",
            schema: "public",
            table: "items",
            column: "code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_items_is_active",
            schema: "public",
            table: "items",
            column: "is_active");

        migrationBuilder.CreateIndex(
            name: "ix_kitchen_stations_code",
            schema: "public",
            table: "kitchen_stations",
            column: "code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_modifier_item",
            schema: "public",
            table: "modifiers",
            column: "item_id");

        migrationBuilder.CreateIndex(
            name: "ix_order_item_details_choice_category_id",
            schema: "public",
            table: "order_item_details",
            column: "choice_category_id");

        migrationBuilder.CreateIndex(
            name: "ix_order_item_details_item_id",
            schema: "public",
            table: "order_item_details",
            column: "item_id");

        migrationBuilder.CreateIndex(
            name: "ix_order_item_details_order_item_id",
            schema: "public",
            table: "order_item_details",
            column: "order_item_id");

        migrationBuilder.CreateIndex(
            name: "ix_order_item_kds",
            schema: "public",
            table: "order_items",
            columns: new[] { "kitchen_station_id", "status", "updated_at" });

        migrationBuilder.CreateIndex(
            name: "ix_order_item_original",
            schema: "public",
            table: "order_items",
            column: "original_order_item_id");

        migrationBuilder.CreateIndex(
            name: "ix_order_item_ticket_status",
            schema: "public",
            table: "order_items",
            columns: new[] { "ticket_id", "status" });

        migrationBuilder.CreateIndex(
            name: "ix_order_items_cancellation_reason_id",
            schema: "public",
            table: "order_items",
            column: "cancellation_reason_id");

        migrationBuilder.CreateIndex(
            name: "ix_order_items_cancelled_by_staff_id",
            schema: "public",
            table: "order_items",
            column: "cancelled_by_staff_id");

        migrationBuilder.CreateIndex(
            name: "ix_order_items_item_id",
            schema: "public",
            table: "order_items",
            column: "item_id");

        migrationBuilder.CreateIndex(
            name: "ix_order_items_order_id",
            schema: "public",
            table: "order_items",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "ix_order_items_status",
            schema: "public",
            table: "order_items",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_order_items_ticket_id",
            schema: "public",
            table: "order_items",
            column: "ticket_id");

        migrationBuilder.CreateIndex(
            name: "ix_order_items_uom_id",
            schema: "public",
            table: "order_items",
            column: "uom_id");

        migrationBuilder.CreateIndex(
            name: "ix_order_status_updated",
            schema: "public",
            table: "orders",
            columns: new[] { "status", "updated_at" });

        migrationBuilder.CreateIndex(
            name: "ix_orders_created_by_staff_id",
            schema: "public",
            table: "orders",
            column: "created_by_staff_id");

        migrationBuilder.CreateIndex(
            name: "ix_orders_ticket_id",
            schema: "public",
            table: "orders",
            column: "ticket_id");

        migrationBuilder.CreateIndex(
            name: "ux_order_ticket_sequence",
            schema: "public",
            table: "orders",
            columns: new[] { "ticket_id", "order_number" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_payment_methods_code",
            schema: "public",
            table: "payment_methods",
            column: "code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_permission_groups_code",
            schema: "public",
            table: "permission_groups",
            column: "code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_permissions_code",
            schema: "public",
            table: "permissions",
            column: "code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_permissions_permission_group_id",
            schema: "public",
            table: "permissions",
            column: "permission_group_id");

        migrationBuilder.CreateIndex(
            name: "ix_price_entries_item_id",
            schema: "public",
            table: "price_entries",
            column: "item_id");

        migrationBuilder.CreateIndex(
            name: "ix_price_entries_price_variant_id",
            schema: "public",
            table: "price_entries",
            column: "price_variant_id");

        migrationBuilder.CreateIndex(
            name: "ux_price_entry_variant_item",
            schema: "public",
            table: "price_entries",
            columns: new[] { "price_variant_id", "item_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_price_tables_code",
            schema: "public",
            table: "price_tables",
            column: "code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_price_variant_area_area",
            schema: "public",
            table: "price_variant_areas",
            column: "area_id");

        migrationBuilder.CreateIndex(
            name: "ix_price_variant_active",
            schema: "public",
            table: "price_variants",
            columns: new[] { "price_table_id", "is_active" });

        migrationBuilder.CreateIndex(
            name: "ix_price_variants_price_table_id",
            schema: "public",
            table: "price_variants",
            column: "price_table_id");

        migrationBuilder.CreateIndex(
            name: "ux_price_variant_table_code",
            schema: "public",
            table: "price_variants",
            columns: new[] { "price_table_id", "code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_printer_counter",
            schema: "public",
            table: "printers",
            column: "counter_id");

        migrationBuilder.CreateIndex(
            name: "ix_printer_kitchen_station",
            schema: "public",
            table: "printers",
            column: "kitchen_station_id");

        migrationBuilder.CreateIndex(
            name: "ix_printers_code",
            schema: "public",
            table: "printers",
            column: "code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_printers_type",
            schema: "public",
            table: "printers",
            column: "type");

        migrationBuilder.CreateIndex(
            name: "ix_rag_document_chunk_embedding",
            schema: "public",
            table: "rag_document_chunks",
            column: "embedding")
            .Annotation("Npgsql:IndexMethod", "hnsw")
            .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

        migrationBuilder.CreateIndex(
            name: "ix_rag_document_chunk_source",
            schema: "public",
            table: "rag_document_chunks",
            columns: new[] { "source_type", "source_ref" });

        migrationBuilder.CreateIndex(
            name: "ix_rag_document_chunks_is_active",
            schema: "public",
            table: "rag_document_chunks",
            column: "is_active");

        migrationBuilder.CreateIndex(
            name: "ix_rag_document_chunks_source_type",
            schema: "public",
            table: "rag_document_chunks",
            column: "source_type");

        migrationBuilder.CreateIndex(
            name: "ix_reservation_phone",
            schema: "public",
            table: "reservations",
            column: "customer_phone");

        migrationBuilder.CreateIndex(
            name: "ix_reservation_status_target_time",
            schema: "public",
            table: "reservations",
            columns: new[] { "status", "target_time" });

        migrationBuilder.CreateIndex(
            name: "ix_reservation_table_active",
            schema: "public",
            table: "reservations",
            columns: new[] { "table_id", "status", "target_time" });

        migrationBuilder.CreateIndex(
            name: "ix_reservations_cancellation_reason_id",
            schema: "public",
            table: "reservations",
            column: "cancellation_reason_id");

        migrationBuilder.CreateIndex(
            name: "ix_reservations_code",
            schema: "public",
            table: "reservations",
            column: "code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_reservations_created_by_staff_id",
            schema: "public",
            table: "reservations",
            column: "created_by_staff_id");

        migrationBuilder.CreateIndex(
            name: "ix_reservations_linked_ticket_id",
            schema: "public",
            table: "reservations",
            column: "linked_ticket_id");

        migrationBuilder.CreateIndex(
            name: "ix_reservations_table_id",
            schema: "public",
            table: "reservations",
            column: "table_id");

        migrationBuilder.CreateIndex(
            name: "ix_roles_code",
            schema: "public",
            table: "roles",
            column: "code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_set_menu_detail_choice_category",
            schema: "public",
            table: "set_menu_details",
            column: "choice_category_id");

        migrationBuilder.CreateIndex(
            name: "ix_set_menu_detail_component",
            schema: "public",
            table: "set_menu_details",
            column: "component_item_id");

        migrationBuilder.CreateIndex(
            name: "ix_set_menu_detail_order",
            schema: "public",
            table: "set_menu_details",
            columns: new[] { "set_menu_item_id", "display_order" });

        migrationBuilder.CreateIndex(
            name: "ix_set_menu_details_set_menu_item_id",
            schema: "public",
            table: "set_menu_details",
            column: "set_menu_item_id");

        migrationBuilder.CreateIndex(
            name: "ix_shifts_code",
            schema: "public",
            table: "shifts",
            column: "code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_staff_account_permission_permission_id",
            schema: "public",
            table: "staff_account_permissions",
            column: "permission_id");

        migrationBuilder.CreateIndex(
            name: "ix_staff_account_login",
            schema: "public",
            table: "staff_accounts",
            columns: new[] { "is_active", "is_locked" });

        migrationBuilder.CreateIndex(
            name: "ix_staff_accounts_is_active",
            schema: "public",
            table: "staff_accounts",
            column: "is_active");

        migrationBuilder.CreateIndex(
            name: "ix_staff_accounts_role_id",
            schema: "public",
            table: "staff_accounts",
            column: "role_id");

        migrationBuilder.CreateIndex(
            name: "ix_staff_accounts_username",
            schema: "public",
            table: "staff_accounts",
            column: "username",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_stock_movement_item_time",
            schema: "public",
            table: "stock_movements",
            columns: new[] { "item_id", "created_at" });

        migrationBuilder.CreateIndex(
            name: "ix_stock_movement_ref",
            schema: "public",
            table: "stock_movements",
            columns: new[] { "reference_type", "reference_id" });

        migrationBuilder.CreateIndex(
            name: "ix_stock_movement_time",
            schema: "public",
            table: "stock_movements",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "ix_stock_movements_created_by_staff_id",
            schema: "public",
            table: "stock_movements",
            column: "created_by_staff_id");

        migrationBuilder.CreateIndex(
            name: "ix_stock_movements_movement_type",
            schema: "public",
            table: "stock_movements",
            column: "movement_type");

        migrationBuilder.CreateIndex(
            name: "ix_table_area_updated",
            schema: "public",
            table: "tables",
            columns: new[] { "area_id", "updated_at" });

        migrationBuilder.CreateIndex(
            name: "ix_tables_area_id",
            schema: "public",
            table: "tables",
            column: "area_id");

        migrationBuilder.CreateIndex(
            name: "ux_table_area_code",
            schema: "public",
            table: "tables",
            columns: new[] { "area_id", "code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_ticket_item_sum_render",
            schema: "public",
            table: "ticket_item_sums",
            columns: new[] { "ticket_id", "display_order" });

        migrationBuilder.CreateIndex(
            name: "ix_ticket_item_sums_item_id",
            schema: "public",
            table: "ticket_item_sums",
            column: "item_id");

        migrationBuilder.CreateIndex(
            name: "ix_ticket_item_sums_ticket_id",
            schema: "public",
            table: "ticket_item_sums",
            column: "ticket_id");

        migrationBuilder.CreateIndex(
            name: "ix_ticket_item_sums_uom_id",
            schema: "public",
            table: "ticket_item_sums",
            column: "uom_id");

        migrationBuilder.CreateIndex(
            name: "ux_ticket_item_sum_bucket",
            schema: "public",
            table: "ticket_item_sums",
            columns: new[] { "ticket_id", "item_id", "uom_id", "unit_price", "discount_percent", "choice_price_per_unit", "vat_percent", "service_charge_percent" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_ticket_payment_detail_ticket_status",
            schema: "public",
            table: "ticket_payment_details",
            columns: new[] { "ticket_id", "status" });

        migrationBuilder.CreateIndex(
            name: "ix_ticket_payment_details_payment_method_id",
            schema: "public",
            table: "ticket_payment_details",
            column: "payment_method_id");

        migrationBuilder.CreateIndex(
            name: "ix_ticket_payment_details_processed_by_staff_id",
            schema: "public",
            table: "ticket_payment_details",
            column: "processed_by_staff_id");

        migrationBuilder.CreateIndex(
            name: "ix_ticket_payment_details_status",
            schema: "public",
            table: "ticket_payment_details",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_ticket_payment_details_ticket_id",
            schema: "public",
            table: "ticket_payment_details",
            column: "ticket_id");

        migrationBuilder.CreateIndex(
            name: "ux_ticket_payment_detail_tx_ref",
            schema: "public",
            table: "ticket_payment_details",
            columns: new[] { "ticket_id", "transaction_ref" },
            unique: true,
            filter: "transaction_ref IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ix_ticket_area_status",
            schema: "public",
            table: "tickets",
            columns: new[] { "area_id", "status" });

        migrationBuilder.CreateIndex(
            name: "ix_ticket_counter_active",
            schema: "public",
            table: "tickets",
            columns: new[] { "counter_id", "status", "updated_at" });

        migrationBuilder.CreateIndex(
            name: "ix_tickets_cancellation_reason_id",
            schema: "public",
            table: "tickets",
            column: "cancellation_reason_id");

        migrationBuilder.CreateIndex(
            name: "ix_tickets_cash_drawer_session_id",
            schema: "public",
            table: "tickets",
            column: "cash_drawer_session_id");

        migrationBuilder.CreateIndex(
            name: "ix_tickets_code",
            schema: "public",
            table: "tickets",
            column: "code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_tickets_discount_policy_id",
            schema: "public",
            table: "tickets",
            column: "discount_policy_id");

        migrationBuilder.CreateIndex(
            name: "ix_tickets_manager_staff_id",
            schema: "public",
            table: "tickets",
            column: "manager_staff_id");

        migrationBuilder.CreateIndex(
            name: "ix_tickets_shift_id",
            schema: "public",
            table: "tickets",
            column: "shift_id");

        migrationBuilder.CreateIndex(
            name: "ix_tickets_status",
            schema: "public",
            table: "tickets",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_tickets_table_id",
            schema: "public",
            table: "tickets",
            column: "table_id");

        migrationBuilder.CreateIndex(
            name: "ix_tickets_waiter_staff_id",
            schema: "public",
            table: "tickets",
            column: "waiter_staff_id");

        migrationBuilder.CreateIndex(
            name: "ux_ticket_guest_qr_token",
            schema: "public",
            table: "tickets",
            column: "guest_qr_token",
            unique: true,
            filter: "guest_qr_token IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ix_uoms_code",
            schema: "public",
            table: "uoms",
            column: "code",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ai_notifications",
            schema: "public");

        migrationBuilder.DropTable(
            name: "ai_tool_call_logs",
            schema: "public");

        migrationBuilder.DropTable(
            name: "area_menu_categories",
            schema: "public");

        migrationBuilder.DropTable(
            name: "audit_logs",
            schema: "public");

        migrationBuilder.DropTable(
            name: "bom_lines",
            schema: "public");

        migrationBuilder.DropTable(
            name: "cart_item_details",
            schema: "public");

        migrationBuilder.DropTable(
            name: "cash_drawer_cash_counts",
            schema: "public");

        migrationBuilder.DropTable(
            name: "config_values",
            schema: "public");

        migrationBuilder.DropTable(
            name: "discount_policy_conditions",
            schema: "public");

        migrationBuilder.DropTable(
            name: "domain_versions",
            schema: "public");

        migrationBuilder.DropTable(
            name: "e_invoices",
            schema: "public");

        migrationBuilder.DropTable(
            name: "item_categories",
            schema: "public");

        migrationBuilder.DropTable(
            name: "item_stocks",
            schema: "public");

        migrationBuilder.DropTable(
            name: "item_uom_conversions",
            schema: "public");

        migrationBuilder.DropTable(
            name: "modifiers",
            schema: "public");

        migrationBuilder.DropTable(
            name: "order_item_details",
            schema: "public");

        migrationBuilder.DropTable(
            name: "outbox_message_consumers",
            schema: "public");

        migrationBuilder.DropTable(
            name: "outbox_messages",
            schema: "public");

        migrationBuilder.DropTable(
            name: "price_entries",
            schema: "public");

        migrationBuilder.DropTable(
            name: "price_variant_areas",
            schema: "public");

        migrationBuilder.DropTable(
            name: "printers",
            schema: "public");

        migrationBuilder.DropTable(
            name: "rag_document_chunks",
            schema: "public");

        migrationBuilder.DropTable(
            name: "reservations",
            schema: "public");

        migrationBuilder.DropTable(
            name: "set_menu_details",
            schema: "public");

        migrationBuilder.DropTable(
            name: "staff_account_permissions",
            schema: "public");

        migrationBuilder.DropTable(
            name: "stock_movements",
            schema: "public");

        migrationBuilder.DropTable(
            name: "ticket_item_sums",
            schema: "public");

        migrationBuilder.DropTable(
            name: "ticket_payment_details",
            schema: "public");

        migrationBuilder.DropTable(
            name: "ai_messages",
            schema: "public");

        migrationBuilder.DropTable(
            name: "cart_items",
            schema: "public");

        migrationBuilder.DropTable(
            name: "denominations",
            schema: "public");

        migrationBuilder.DropTable(
            name: "categories",
            schema: "public");

        migrationBuilder.DropTable(
            name: "order_items",
            schema: "public");

        migrationBuilder.DropTable(
            name: "price_variants",
            schema: "public");

        migrationBuilder.DropTable(
            name: "choice_categories",
            schema: "public");

        migrationBuilder.DropTable(
            name: "set_menus",
            schema: "public");

        migrationBuilder.DropTable(
            name: "permissions",
            schema: "public");

        migrationBuilder.DropTable(
            name: "payment_methods",
            schema: "public");

        migrationBuilder.DropTable(
            name: "ai_conversations",
            schema: "public");

        migrationBuilder.DropTable(
            name: "orders",
            schema: "public");

        migrationBuilder.DropTable(
            name: "price_tables",
            schema: "public");

        migrationBuilder.DropTable(
            name: "items",
            schema: "public");

        migrationBuilder.DropTable(
            name: "permission_groups",
            schema: "public");

        migrationBuilder.DropTable(
            name: "tickets",
            schema: "public");

        migrationBuilder.DropTable(
            name: "kitchen_stations",
            schema: "public");

        migrationBuilder.DropTable(
            name: "uoms",
            schema: "public");

        migrationBuilder.DropTable(
            name: "cancellation_reasons",
            schema: "public");

        migrationBuilder.DropTable(
            name: "cash_drawer_sessions",
            schema: "public");

        migrationBuilder.DropTable(
            name: "discount_policies",
            schema: "public");

        migrationBuilder.DropTable(
            name: "shifts",
            schema: "public");

        migrationBuilder.DropTable(
            name: "tables",
            schema: "public");

        migrationBuilder.DropTable(
            name: "staff_accounts",
            schema: "public");

        migrationBuilder.DropTable(
            name: "areas",
            schema: "public");

        migrationBuilder.DropTable(
            name: "roles",
            schema: "public");

        migrationBuilder.DropTable(
            name: "counters",
            schema: "public");
    }
}
