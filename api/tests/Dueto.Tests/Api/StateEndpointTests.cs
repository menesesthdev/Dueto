using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dueto.Core.State;
using Dueto.Tests.Infrastructure;

namespace Dueto.Tests.Api;

[Collection(PostgresCollection.Name)]
public class StateEndpointTests(PostgresApiFactory factory)
{
    private sealed record VersionBody(long Version);

    private static Task<HttpResponseMessage> Set(HttpClient client, string wallpaperId) =>
        client.PutAsJsonAsync("/api/state", new { wallpaperId });

    private static Task<HttpResponseMessage> Apply(HttpClient client, string wallpaperId, long version) =>
        client.PostAsJsonAsync("/api/state/applied", new { wallpaperId, version });

    private static async Task<StateSnapshot> GetState(HttpClient client) =>
        (await client.GetFromJsonAsync<StateSnapshot>("/api/state"))!;

    [Fact]
    public async Task State_requires_a_token()
    {
        var response = await factory.CreateClient().GetAsync("/api/state");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task State_rejects_an_invalid_token()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/state")).StatusCode);
    }

    [Fact]
    public async Task Access_token_in_the_query_string_is_not_accepted_outside_the_hub()
    {
        var devices = await PairedDevices.CreateAsync(factory);

        var response = await factory.CreateClient().GetAsync($"/api/state?access_token={devices.DesktopToken}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task New_pair_has_version_zero_and_no_wallpaper()
    {
        var devices = await PairedDevices.CreateAsync(factory);

        var state = await GetState(devices.Desktop);

        Assert.Equal(0, state.Version);
        Assert.Null(state.WallpaperId);
        Assert.Equal(2, state.Applied.Count);
        Assert.All(state.Applied, a => Assert.Null(a.Version));
    }

    [Fact]
    public async Task Each_put_increments_the_version()
    {
        var devices = await PairedDevices.CreateAsync(factory);

        var first = await (await Set(devices.Desktop, "aurora-dunes")).Content.ReadFromJsonAsync<VersionBody>();
        var second = await (await Set(devices.Desktop, "neon-tide")).Content.ReadFromJsonAsync<VersionBody>();
        var same = await (await Set(devices.Desktop, "neon-tide")).Content.ReadFromJsonAsync<VersionBody>();

        Assert.Equal([1, 2, 3], [first!.Version, second!.Version, same!.Version]);

        var state = await GetState(devices.Mobile);
        Assert.Equal(3, state.Version);
        Assert.Equal("neon-tide", state.WallpaperId);
        Assert.Equal(devices.DesktopId, state.OriginDeviceId);
    }

    [Fact]
    public async Task Concurrent_puts_get_distinct_consecutive_versions()
    {
        var devices = await PairedDevices.CreateAsync(factory);

        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(i =>
            Set(i % 2 == 0 ? devices.Desktop : devices.Mobile, "aurora-dunes")));
        var versions = new List<long>();
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            versions.Add((await response.Content.ReadFromJsonAsync<VersionBody>())!.Version);
        }

        Assert.Equal(Enumerable.Range(1, 20).Select(i => (long)i), versions.Order());
        Assert.Equal(20, (await GetState(devices.Desktop)).Version);
    }

    [Fact]
    public async Task Put_from_desktop_marks_the_desktop_as_applied_but_not_the_mobile()
    {
        var devices = await PairedDevices.CreateAsync(factory);

        await Set(devices.Desktop, "aurora-dunes");
        var state = await GetState(devices.Desktop);

        var desktop = state.Applied.Single(a => a.Kind == "desktop");
        var mobile = state.Applied.Single(a => a.Kind == "mobile");
        Assert.Equal(1, desktop.Version);
        Assert.Equal("aurora-dunes", desktop.WallpaperId);
        Assert.Null(mobile.Version);
    }

    [Fact]
    public async Task Put_from_mobile_stays_pending_until_the_mobile_confirms()
    {
        var devices = await PairedDevices.CreateAsync(factory);

        await Set(devices.Mobile, "neon-tide");
        var pending = await GetState(devices.Mobile);

        Assert.Null(pending.Applied.Single(a => a.Kind == "mobile").Version);
        Assert.Null(pending.Applied.Single(a => a.Kind == "desktop").Version);

        Assert.Equal(HttpStatusCode.NoContent, (await Apply(devices.Mobile, "neon-tide", 1)).StatusCode);
        Assert.Equal(1, (await GetState(devices.Mobile)).Applied.Single(a => a.Kind == "mobile").Version);
    }

    [Fact]
    public async Task Put_rejects_unknown_and_malformed_wallpapers()
    {
        var devices = await PairedDevices.CreateAsync(factory);

        Assert.Equal(HttpStatusCode.NotFound, (await Set(devices.Desktop, "does-not-exist")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Set(devices.Desktop, "Bad_Id")).StatusCode);
        Assert.Equal(0, (await GetState(devices.Desktop)).Version);
    }

    [Fact]
    public async Task Applied_is_idempotent()
    {
        var devices = await PairedDevices.CreateAsync(factory);
        await Set(devices.Desktop, "neon-tide");

        Assert.Equal(HttpStatusCode.NoContent, (await Apply(devices.Mobile, "neon-tide", 1)).StatusCode);
        var firstAppliedAt = (await GetState(devices.Mobile)).Applied.Single(a => a.Kind == "mobile").AppliedAt;

        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(HttpStatusCode.NoContent, (await Apply(devices.Mobile, "neon-tide", 1)).StatusCode);

        var mobile = (await GetState(devices.Mobile)).Applied.Single(a => a.Kind == "mobile");
        Assert.Equal(1, mobile.Version);
        Assert.Equal(firstAppliedAt, mobile.AppliedAt);
    }

    [Fact]
    public async Task Applied_never_goes_back_to_an_older_version()
    {
        var devices = await PairedDevices.CreateAsync(factory);
        await Set(devices.Desktop, "aurora-dunes");
        await Set(devices.Desktop, "neon-tide");

        await Apply(devices.Mobile, "neon-tide", 2);
        Assert.Equal(HttpStatusCode.NoContent, (await Apply(devices.Mobile, "aurora-dunes", 1)).StatusCode);

        var mobile = (await GetState(devices.Mobile)).Applied.Single(a => a.Kind == "mobile");
        Assert.Equal(2, mobile.Version);
        Assert.Equal("neon-tide", mobile.WallpaperId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(2)]
    public async Task Applied_rejects_versions_outside_the_valid_range(long version)
    {
        var devices = await PairedDevices.CreateAsync(factory);
        await Set(devices.Desktop, "aurora-dunes");

        Assert.Equal(HttpStatusCode.BadRequest, (await Apply(devices.Mobile, "aurora-dunes", version)).StatusCode);
    }

    [Fact]
    public async Task Applied_rejects_a_wallpaper_that_does_not_match_the_current_version()
    {
        var devices = await PairedDevices.CreateAsync(factory);
        await Set(devices.Desktop, "aurora-dunes");

        Assert.Equal(HttpStatusCode.Conflict, (await Apply(devices.Mobile, "neon-tide", 1)).StatusCode);
    }

    [Fact]
    public async Task Pairs_are_isolated_from_each_other()
    {
        var first = await PairedDevices.CreateAsync(factory);
        var second = await PairedDevices.CreateAsync(factory);

        await Set(first.Desktop, "aurora-dunes");

        var other = await GetState(second.Desktop);
        Assert.Equal(0, other.Version);
        Assert.Null(other.WallpaperId);
        Assert.DoesNotContain(other.Applied, a => a.DeviceId == first.DesktopId);
    }
}
