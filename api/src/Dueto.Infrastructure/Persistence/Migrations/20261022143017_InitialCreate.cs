using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dueto.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "device_applied",
            columns: table => new
            {
                device_id = table.Column<Guid>(type: "uuid", nullable: false),
                wallpaper_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false),
                applied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_device_applied", x => x.device_id);
            });

        migrationBuilder.CreateTable(
            name: "pair_state",
            columns: table => new
            {
                pair_id = table.Column<Guid>(type: "uuid", nullable: false),
                wallpaper_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                version = table.Column<long>(type: "bigint", nullable: false),
                origin_device_id = table.Column<Guid>(type: "uuid", nullable: true),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_pair_state", x => x.pair_id);
            });

        migrationBuilder.CreateTable(
            name: "pairing_codes",
            columns: table => new
            {
                code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                pair_id = table.Column<Guid>(type: "uuid", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_pairing_codes", x => x.code);
            });

        migrationBuilder.CreateTable(
            name: "pairs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_pairs", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "push_subscriptions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                device_id = table.Column<Guid>(type: "uuid", nullable: false),
                endpoint = table.Column<string>(type: "text", nullable: false),
                p256dh = table.Column<string>(type: "text", nullable: false),
                auth = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_push_subscriptions", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "reminders",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                device_id = table.Column<Guid>(type: "uuid", nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false),
                sequence = table.Column<int>(type: "integer", nullable: false),
                due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_reminders", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "wallpapers",
            columns: table => new
            {
                id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                desktop_url = table.Column<string>(type: "text", nullable: false),
                desktop_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                mobile_url = table.Column<string>(type: "text", nullable: true),
                mobile_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                thumbnail_url = table.Column<string>(type: "text", nullable: false),
                tags = table.Column<string[]>(type: "text[]", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_wallpapers", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "devices",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                pair_id = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_devices", x => x.id);
                table.ForeignKey(
                    name: "FK_devices_pairs_pair_id",
                    column: x => x.pair_id,
                    principalTable: "pairs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_devices_pair_id",
            table: "devices",
            column: "pair_id");

        migrationBuilder.CreateIndex(
            name: "ux_devices_token_hash",
            table: "devices",
            column: "token_hash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_pairing_codes_expires_at",
            table: "pairing_codes",
            column: "expires_at");

        migrationBuilder.CreateIndex(
            name: "ix_push_subscriptions_device_id",
            table: "push_subscriptions",
            column: "device_id");

        migrationBuilder.CreateIndex(
            name: "ux_push_subscriptions_endpoint",
            table: "push_subscriptions",
            column: "endpoint",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_reminders_device_id",
            table: "reminders",
            column: "device_id");

        migrationBuilder.CreateIndex(
            name: "ix_reminders_status_due_at",
            table: "reminders",
            columns: new[] { "status", "due_at" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "device_applied");

        migrationBuilder.DropTable(
            name: "devices");

        migrationBuilder.DropTable(
            name: "pair_state");

        migrationBuilder.DropTable(
            name: "pairing_codes");

        migrationBuilder.DropTable(
            name: "push_subscriptions");

        migrationBuilder.DropTable(
            name: "reminders");

        migrationBuilder.DropTable(
            name: "wallpapers");

        migrationBuilder.DropTable(
            name: "pairs");
    }
}
