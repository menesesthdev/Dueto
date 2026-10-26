using System.Net;
using System.Net.Http.Json;
using Dueto.Core.Pairing;
using Dueto.Infrastructure.Persistence;
using Dueto.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dueto.Tests.Api;

[Collection(PostgresCollection.Name)]
public class PairingEndpointTests(PostgresApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private Task<HttpResponseMessage> Start(string kind = "desktop") =>
        _client.PostAsJsonAsync("/api/pairing/start", new { deviceName = "PC", kind });

    private Task<HttpResponseMessage> Complete(string code, string kind = "mobile") =>
        _client.PostAsJsonAsync("/api/pairing/complete", new { code, deviceName = "iPhone", kind });

    [Fact]
    public async Task Start_returns_token_six_digit_code_and_five_minute_expiry()
    {
        var response = await Start();
        var body = await response.Content.ReadFromJsonAsync<PairedDevices.StartBody>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(PairingCodes.IsValidFormat(body!.Code));
        Assert.False(string.IsNullOrEmpty(body.DeviceToken));
        Assert.Equal(TimeSpan.FromMinutes(5), body.ExpiresAt - factory.Clock.GetUtcNow());
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Database_stores_only_the_token_hash()
    {
        var started = (await (await Start()).Content.ReadFromJsonAsync<PairedDevices.StartBody>())!;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DuetoDbContext>();
        var hashes = await db.Devices.Select(d => d.TokenHash).ToListAsync();

        Assert.DoesNotContain(started.DeviceToken, hashes);
        Assert.Contains(DeviceTokens.Hash(started.DeviceToken), hashes);
    }

    [Fact]
    public async Task Complete_with_valid_code_returns_a_token_that_authenticates()
    {
        var started = (await (await Start()).Content.ReadFromJsonAsync<PairedDevices.StartBody>())!;

        var response = await Complete(started.Code);
        var completed = await response.Content.ReadFromJsonAsync<PairedDevices.CompleteBody>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await PairedDevices.Authenticated(factory, completed!.DeviceToken).GetAsync("/api/state");
        Assert.Equal(HttpStatusCode.OK, state.StatusCode);
    }

    [Fact]
    public async Task Code_is_single_use()
    {
        var started = (await (await Start()).Content.ReadFromJsonAsync<PairedDevices.StartBody>())!;

        Assert.Equal(HttpStatusCode.OK, (await Complete(started.Code)).StatusCode);
        var second = await Complete(started.Code);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Code_used_concurrently_is_accepted_only_once()
    {
        var started = (await (await Start()).Content.ReadFromJsonAsync<PairedDevices.StartBody>())!;

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Complete(started.Code)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
    }

    [Fact]
    public async Task Code_expires_after_the_ttl()
    {
        var started = (await (await Start()).Content.ReadFromJsonAsync<PairedDevices.StartBody>())!;

        factory.Clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        var response = await Complete(started.Code);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Code_still_works_just_before_expiry()
    {
        var started = (await (await Start()).Content.ReadFromJsonAsync<PairedDevices.StartBody>())!;

        factory.Clock.Advance(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(59));

        Assert.Equal(HttpStatusCode.OK, (await Complete(started.Code)).StatusCode);
    }

    [Fact]
    public async Task Unknown_code_is_rejected_with_the_same_error_as_an_expired_one()
    {
        var response = await Complete("000000");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Invalid or expired pairing code.", body);
    }

    [Fact]
    public async Task Pair_cannot_get_two_devices_of_the_same_kind()
    {
        var started = (await (await Start("desktop")).Content.ReadFromJsonAsync<PairedDevices.StartBody>())!;

        var response = await Complete(started.Code, kind: "desktop");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // O conflito não consome o código: o dispositivo certo ainda consegue parear.
        Assert.Equal(HttpStatusCode.OK, (await Complete(started.Code, kind: "mobile")).StatusCode);
    }

    [Fact]
    public async Task Mobile_can_start_and_desktop_can_complete()
    {
        var started = (await (await Start("mobile")).Content.ReadFromJsonAsync<PairedDevices.StartBody>())!;

        Assert.Equal(HttpStatusCode.OK, (await Complete(started.Code, kind: "desktop")).StatusCode);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("abcdef")]
    [InlineData("1234567")]
    [InlineData("")]
    public async Task Complete_rejects_malformed_codes(string code)
    {
        var response = await Complete(code);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("", "desktop")]
    [InlineData("   ", "desktop")]
    [InlineData("PC", "tablet")]
    [InlineData("PC", "")]
    public async Task Start_rejects_invalid_input(string deviceName, string kind)
    {
        var response = await _client.PostAsJsonAsync("/api/pairing/start", new { deviceName, kind });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Start_rejects_overlong_device_name()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/pairing/start",
            new { deviceName = new string('x', 101), kind = "desktop" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Expired_and_used_codes_are_purged_on_the_next_start()
    {
        var started = (await (await Start()).Content.ReadFromJsonAsync<PairedDevices.StartBody>())!;
        factory.Clock.Advance(TimeSpan.FromMinutes(10));

        await Start();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DuetoDbContext>();
        Assert.False(await db.PairingCodes.AnyAsync(c => c.Code == started.Code && c.ExpiresAt < factory.Clock.GetUtcNow()));
    }
}
