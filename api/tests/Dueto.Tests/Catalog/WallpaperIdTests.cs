using Dueto.Core.Catalog;

namespace Dueto.Tests.Catalog;

public class WallpaperIdTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("aurora-dunes")]
    [InlineData("neon-tide-2")]
    [InlineData("0123456789")]
    public void Valid_slugs_are_accepted(string id) => Assert.True(WallpaperId.IsValid(id));

    [Fact]
    public void Slug_with_exactly_64_chars_is_accepted() =>
        Assert.True(WallpaperId.IsValid(new string('a', 64)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Aurora")]
    [InlineData("aurora_dunes")]
    [InlineData("aurora dunes")]
    [InlineData("../etc/passwd")]
    [InlineData("https://evil.example/x")]
    [InlineData("aurora\n")]
    public void Invalid_slugs_are_rejected(string? id) => Assert.False(WallpaperId.IsValid(id));

    [Fact]
    public void Slug_with_65_chars_is_rejected() =>
        Assert.False(WallpaperId.IsValid(new string('a', 65)));
}
