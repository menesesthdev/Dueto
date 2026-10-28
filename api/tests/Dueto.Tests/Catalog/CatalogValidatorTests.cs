using Dueto.Core.Catalog;
using Dueto.Core.Domain;

namespace Dueto.Tests.Catalog;

public class CatalogValidatorTests
{
    private static readonly string[] Hosts = ["github.com"];
    private static readonly string Sha = new('a', 64);
    private const string Url = "https://github.com/owner/repo/releases/download/v1/file.png";

    private static CatalogEntry Static(string id = "aurora-dunes") => new(
        id, "Aurora Dunes", "static", Url, Sha, Url, Sha, Url, ["landscape"], new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));

    private static CatalogEntry Animated(string id = "drift-loop") => new(
        id, "Drift Loop", "animated", Url, Sha, null, null, Url, [], new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero));

    private static CatalogValidationResult Validate(params CatalogEntry[] entries) =>
        CatalogValidator.Validate(new CatalogDocument(entries), Hosts);

    private static void AssertInvalid(CatalogEntry entry, string fieldFragment)
    {
        var result = Validate(entry);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains(fieldFragment));
        Assert.Empty(result.Wallpapers);
    }

    [Fact]
    public void A_valid_static_and_a_valid_animated_entry_are_accepted()
    {
        var result = Validate(Static(), Animated());

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Wallpapers.Count);
        Assert.Equal(WallpaperKind.Animated, result.Wallpapers[1].Kind);
        Assert.Null(result.Wallpapers[1].MobileUrl);
    }

    [Fact]
    public void An_empty_catalog_is_valid() => Assert.True(Validate().IsValid);

    [Fact]
    public void A_missing_wallpapers_array_is_invalid()
    {
        Assert.False(CatalogValidator.Validate(null, Hosts).IsValid);
        Assert.False(CatalogValidator.Validate(new CatalogDocument(null), Hosts).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Aurora")]
    [InlineData("aurora_dunes")]
    [InlineData("aurora\n")]
    public void Invalid_ids_are_rejected(string? id) => AssertInvalid(Static() with { Id = id }, ".id");

    [Fact]
    public void Duplicate_ids_are_rejected()
    {
        var result = Validate(Static(), Static());

        Assert.Contains(result.Errors, e => e.Contains("duplicated"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void A_blank_title_is_rejected(string? title) => AssertInvalid(Static() with { Title = title }, ".title");

    [Fact]
    public void An_overlong_title_is_rejected() => AssertInvalid(Static() with { Title = new string('x', 201) }, ".title");

    [Theory]
    [InlineData(null)]
    [InlineData("video")]
    [InlineData("")]
    public void An_unknown_kind_is_rejected(string? kind) => AssertInvalid(Static() with { Kind = kind }, ".kind");

    [Theory]
    [InlineData("STATIC")]
    [InlineData("Static")]
    public void The_kind_is_case_insensitive(string kind) => Assert.True(Validate(Static() with { Kind = kind }).IsValid);

    [Theory]
    [InlineData("http://github.com/a/b.png")]
    [InlineData("https://example.com/a.png")]
    [InlineData("https://notgithub.com/a.png")]
    [InlineData("https://github.com.evil.example/a.png")]
    [InlineData("https://user:pass@github.com/a.png")]
    [InlineData("ftp://github.com/a.png")]
    [InlineData("not a url")]
    [InlineData(null)]
    public void Urls_outside_the_allowlist_are_rejected(string? url)
    {
        AssertInvalid(Static() with { DesktopUrl = url }, ".desktopUrl");
        AssertInvalid(Static() with { ThumbnailUrl = url }, ".thumbnailUrl");
    }

    [Fact]
    public void Subdomains_of_an_allowed_host_are_accepted() =>
        Assert.True(Validate(Static() with { DesktopUrl = "https://objects.github.com/x.png" }).IsValid);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")] // 64 chars, mas não hexadecimal
    public void A_malformed_sha256_is_rejected(string? sha) => AssertInvalid(Static() with { DesktopSha256 = sha }, ".desktopSha256");

    [Fact]
    public void An_uppercase_sha256_is_accepted_and_normalized_to_lowercase()
    {
        var result = Validate(Static() with { DesktopSha256 = new string('A', 64) });

        Assert.True(result.IsValid);
        Assert.Equal(new string('a', 64), result.Wallpapers[0].DesktopSha256);
    }

    [Fact]
    public void A_static_wallpaper_must_have_a_mobile_file() =>
        AssertInvalid(Static() with { MobileUrl = null, MobileSha256 = null }, "static wallpaper needs mobileUrl");

    [Fact]
    public void An_animated_wallpaper_must_not_have_a_mobile_file() =>
        AssertInvalid(Animated() with { MobileUrl = Url, MobileSha256 = Sha }, "desktop-only");

    [Fact]
    public void The_mobile_url_and_sha_must_come_together() =>
        AssertInvalid(Static() with { MobileSha256 = null }, "must be set together");

    [Theory]
    [InlineData("Landscape")]
    [InlineData("two words")]
    [InlineData("")]
    public void Invalid_tags_are_rejected(string tag) => AssertInvalid(Static() with { Tags = [tag] }, ".tags");

    [Fact]
    public void Too_many_tags_are_rejected() =>
        AssertInvalid(Static() with { Tags = Enumerable.Range(0, 21).Select(i => $"t{i}").ToList() }, ".tags");

    [Fact]
    public void A_missing_created_at_is_rejected() => AssertInvalid(Static() with { CreatedAt = null }, ".createdAt");

    [Fact]
    public void Created_at_is_normalized_to_utc()
    {
        var local = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.FromHours(-3));

        var result = Validate(Static() with { CreatedAt = local });

        Assert.Equal(TimeSpan.Zero, result.Wallpapers[0].CreatedAt.Offset);
        Assert.Equal(local.UtcDateTime, result.Wallpapers[0].CreatedAt.UtcDateTime);
    }

    [Fact]
    public void Every_problem_is_reported_not_just_the_first()
    {
        var result = Validate(Static() with { Id = "Bad", Title = "", DesktopSha256 = "x" });

        Assert.True(result.Errors.Count >= 3);
    }

    [Fact]
    public void One_invalid_entry_makes_the_whole_catalog_invalid()
    {
        var result = Validate(Static(), Static("neon-tide") with { Kind = "nope" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.StartsWith("wallpapers[1]"));
    }

    [Fact]
    public void Too_many_wallpapers_are_rejected()
    {
        var entries = Enumerable.Range(0, CatalogValidator.MaxWallpapers + 1).Select(i => Static($"w-{i}")).ToArray();

        Assert.False(Validate(entries).IsValid);
    }
}
