using System.Net;
using Dueto.Core.Catalog;
using Dueto.Infrastructure.Catalog;
using Dueto.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace Dueto.Tests.Catalog;

/// <summary>Importação do catalog.json contra um Postgres real e vazio, num container próprio.</summary>
public sealed class CatalogImporterTests : IAsyncLifetime
{
    private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly string _directory = Directory.CreateTempSubdirectory("dueto-catalog-").FullName;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = NewContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        Directory.Delete(_directory, recursive: true);
    }

    private DuetoDbContext NewContext() => new(new DbContextOptionsBuilder<DuetoDbContext>()
        .UseNpgsql(_postgres.GetConnectionString())
        .Options);

    private string WriteFile(string json)
    {
        var path = Path.Combine(_directory, $"{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    private static string Entry(string id, string title = "Title", string kind = "static", string sha = Sha, bool mobile = true) => $$"""
        {
          "id": "{{id}}", "title": "{{title}}", "kind": "{{kind}}",
          "desktopUrl": "https://github.com/o/r/releases/download/v1/{{id}}-desktop.png", "desktopSha256": "{{sha}}",
          {{(mobile ? $$"""
          "mobileUrl": "https://github.com/o/r/releases/download/v1/{{id}}-mobile.png", "mobileSha256": "{{sha}}",
          """ : "")}}
          "thumbnailUrl": "https://github.com/o/r/releases/download/v1/{{id}}-thumb.jpg",
          "tags": ["a"], "createdAt": "2026-10-01T12:00:00Z"
        }
        """;

    private static string Catalog(params string[] entries) => $$"""{ "wallpapers": [ {{string.Join(",", entries)}} ] }""";

    private async Task Import(string? file, string[]? hosts = null)
    {
        await using var db = NewContext();
        var importer = new CatalogImporter(
            db,
            Options.Create(new CatalogOptions { ImportFile = file, AllowedHosts = hosts ?? [] }),
            NullLogger<CatalogImporter>.Instance);
        await importer.ImportAsync(CancellationToken.None);
    }

    private async Task<List<Dueto.Core.Domain.Wallpaper>> Stored()
    {
        await using var db = NewContext();
        return await db.Wallpapers.AsNoTracking().OrderBy(w => w.Id).ToListAsync();
    }

    [Fact]
    public async Task Imports_new_wallpapers()
    {
        await Import(WriteFile(Catalog(Entry("aurora"), Entry("drift", kind: "animated", mobile: false))));

        var stored = await Stored();
        Assert.Equal(["aurora", "drift"], stored.Select(w => w.Id));
        Assert.Null(stored[1].MobileUrl);
        Assert.Equal(["a"], stored[0].Tags);
    }

    [Fact]
    public async Task Importing_twice_does_not_duplicate()
    {
        var file = WriteFile(Catalog(Entry("aurora")));

        await Import(file);
        await Import(file);

        Assert.Single(await Stored());
    }

    [Fact]
    public async Task A_changed_entry_is_updated_in_place()
    {
        await Import(WriteFile(Catalog(Entry("aurora", title: "Old"))));
        await Import(WriteFile(Catalog(Entry("aurora", title: "New", sha: new string('b', 64)))));

        var stored = Assert.Single(await Stored());
        Assert.Equal("New", stored.Title);
        Assert.Equal(new string('b', 64), stored.DesktopSha256);
    }

    [Fact]
    public async Task Wallpapers_missing_from_the_file_are_never_deleted()
    {
        await Import(WriteFile(Catalog(Entry("aurora"), Entry("neon"))));
        await Import(WriteFile(Catalog(Entry("aurora"))));

        Assert.Equal(["aurora", "neon"], (await Stored()).Select(w => w.Id));
    }

    [Fact]
    public async Task An_empty_catalog_imports_nothing_and_is_valid()
    {
        await Import(WriteFile(Catalog()));

        Assert.Empty(await Stored());
    }

    [Fact]
    public async Task One_invalid_entry_imports_nothing()
    {
        var file = WriteFile(Catalog(Entry("aurora"), Entry("Bad_Id")));

        var exception = await Assert.ThrowsAsync<CatalogImportException>(() => Import(file));

        Assert.Contains("wallpapers[1].id", exception.Message);
        Assert.Empty(await Stored()); // tudo ou nada: a entrada boa também não entrou
    }

    [Fact]
    public async Task A_missing_file_fails_loudly()
    {
        var exception = await Assert.ThrowsAsync<CatalogImportException>(() => Import(Path.Combine(_directory, "nope.json")));

        Assert.Contains("not found", exception.Message);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    public async Task Malformed_json_fails_loudly(string content) =>
        await Assert.ThrowsAsync<CatalogImportException>(() => Import(WriteFile(content)));

    [Fact]
    public async Task A_misspelled_field_is_an_error_instead_of_being_ignored()
    {
        var json = Catalog(Entry("aurora")).Replace("\"desktopUrl\"", "\"desktopUrI\"");

        await Assert.ThrowsAsync<CatalogImportException>(() => Import(WriteFile(json)));
        Assert.Empty(await Stored());
    }

    [Fact]
    public async Task Comments_and_trailing_commas_are_allowed_so_the_file_can_be_annotated()
    {
        var json = "// nota do dono\n" + Catalog(Entry("aurora")).Replace("]", ",]");

        await Import(WriteFile(json));

        Assert.Single(await Stored());
    }

    [Fact]
    public async Task A_file_over_the_size_limit_is_rejected()
    {
        var file = WriteFile(Catalog() + new string(' ', 1_048_577));

        var exception = await Assert.ThrowsAsync<CatalogImportException>(() => Import(file));

        Assert.Contains("larger", exception.Message);
    }

    [Fact]
    public async Task Urls_must_belong_to_an_allowed_host()
    {
        var file = WriteFile(Catalog(Entry("aurora")));

        await Assert.ThrowsAsync<CatalogImportException>(() => Import(file, hosts: ["example.com"]));
        await Import(file, hosts: ["github.com"]); // o host configurado substitui o padrão
        Assert.Single(await Stored());
    }

    [Fact]
    public async Task No_file_configured_means_no_import()
    {
        await Import(null);
        await Import("   ");

        Assert.Empty(await Stored());
    }

    [Fact]
    public async Task The_error_message_lists_the_problems_but_not_the_file_content()
    {
        var file = WriteFile(Catalog(Entry("Bad_Id", title: "SECRET-TITLE-123")));

        var exception = await Assert.ThrowsAsync<CatalogImportException>(() => Import(file));

        Assert.Contains("wallpapers[0].id", exception.Message);
        Assert.DoesNotContain("SECRET-TITLE-123", exception.Message);
    }

    [Fact]
    public void The_app_refuses_to_start_with_an_invalid_catalog_file()
    {
        using var factory = new Infrastructure.ConfiguredApiFactory(new Dictionary<string, string?>
        {
            ["Catalog:ImportFile"] = WriteFile("{ \"wallpapers\": [ { \"id\": \"Bad_Id\" } ] }"),
        });

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    [Fact]
    public void Invalid_allowed_hosts_make_the_app_refuse_to_start()
    {
        using var factory = new Infrastructure.ConfiguredApiFactory(new Dictionary<string, string?>
        {
            ["Catalog:AllowedHosts:0"] = "https://github.com/path",
        });

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    [Fact]
    public async Task The_status_of_an_unconfigured_import_is_harmless_for_the_http_surface()
    {
        using var factory = new Infrastructure.ConfiguredApiFactory(new Dictionary<string, string?>());

        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync("/health")).StatusCode);
    }
}
