using System.Net;
using System.Net.Http.Json;
using Dueto.Core.Catalog;
using Dueto.Tests.Infrastructure;

namespace Dueto.Tests.Api;

/// <summary>Testes de integração do catálogo, contra um Postgres real em container.</summary>
[Collection(PostgresCollection.Name)]
public class WallpaperEndpointTests
{
    private readonly HttpClient _client;

    public WallpaperEndpointTests(PostgresApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task List_returns_seeded_wallpapers_newest_first()
    {
        var response = await _client.GetAsync("/api/wallpapers");
        var wallpapers = await response.Content.ReadFromJsonAsync<List<WallpaperDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["drift-loop", "neon-tide", "aurora-dunes"], wallpapers!.Select(w => w.Id));
    }

    [Fact]
    public async Task Get_by_id_returns_the_wallpaper()
    {
        var response = await _client.GetAsync("/api/wallpapers/aurora-dunes");
        var wallpaper = await response.Content.ReadFromJsonAsync<WallpaperDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Aurora Dunes", wallpaper!.Title);
        Assert.Equal("static", wallpaper.Kind);
        Assert.Equal(["landscape", "dark"], wallpaper.Tags);
        Assert.NotNull(wallpaper.MobileUrl);
    }

    [Fact]
    public async Task Animated_wallpaper_has_no_mobile_file()
    {
        var wallpaper = await _client.GetFromJsonAsync<WallpaperDto>("/api/wallpapers/drift-loop");

        Assert.Equal("animated", wallpaper!.Kind);
        Assert.Null(wallpaper.MobileUrl);
        Assert.Null(wallpaper.MobileSha256);
    }

    [Fact]
    public async Task Unknown_id_returns_404_problem_details()
    {
        var response = await _client.GetAsync("/api/wallpapers/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("Aurora")]
    [InlineData("aurora_dunes")]
    [InlineData("a b")]
    public async Task Invalid_id_returns_400_problem_details(string id)
    {
        var response = await _client.GetAsync($"/api/wallpapers/{Uri.EscapeDataString(id)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Problem_details_do_not_leak_internal_information()
    {
        var response = await _client.GetAsync("/api/wallpapers/does-not-exist");
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Exception", body);
        Assert.DoesNotContain("StackTrace", body);
    }
}
