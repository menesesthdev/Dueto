using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Dueto.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class SeedSampleWallpapers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.InsertData(
            table: "wallpapers",
            columns: new[] { "id", "created_at", "desktop_sha256", "desktop_url", "kind", "mobile_sha256", "mobile_url", "tags", "thumbnail_url", "title" },
            values: new object[,]
            {
                { "aurora-dunes", new DateTimeOffset(new DateTime(2026, 10, 1, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "https://example.com/wallpapers/aurora-dunes-desktop.png", "Static", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "https://example.com/wallpapers/aurora-dunes-mobile.png", new[] { "landscape", "dark" }, "https://example.com/wallpapers/aurora-dunes-thumb.jpg", "Aurora Dunes" },
                { "drift-loop", new DateTimeOffset(new DateTime(2026, 10, 3, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee", "https://example.com/wallpapers/drift-loop-desktop.mp4", "Animated", null, null, new[] { "animated", "calm" }, "https://example.com/wallpapers/drift-loop-thumb.jpg", "Drift Loop" },
                { "neon-tide", new DateTimeOffset(new DateTime(2026, 10, 2, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc", "https://example.com/wallpapers/neon-tide-desktop.png", "Static", "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd", "https://example.com/wallpapers/neon-tide-mobile.png", new[] { "abstract", "vibrant" }, "https://example.com/wallpapers/neon-tide-thumb.jpg", "Neon Tide" }
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            table: "wallpapers",
            keyColumn: "id",
            keyValue: "aurora-dunes");

        migrationBuilder.DeleteData(
            table: "wallpapers",
            keyColumn: "id",
            keyValue: "drift-loop");

        migrationBuilder.DeleteData(
            table: "wallpapers",
            keyColumn: "id",
            keyValue: "neon-tide");
    }
}
