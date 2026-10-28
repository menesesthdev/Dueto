using System.Net;
using System.Net.Http.Json;
using Dueto.Core.Domain;
using Dueto.Infrastructure.Persistence;
using Dueto.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dueto.Tests.Api;

[Collection(PostgresCollection.Name)]
public class PushSubscriptionEndpointTests(PostgresApiFactory factory)
{
    internal static string NewEndpoint() => $"https://web.push.apple.com/{Guid.NewGuid():N}";

    internal static object Body(string endpoint, string p256dh = "BNcRdreALRFXTkOOUHK1EtK2wtaz5Ry4YfYCA_0QTpQtUbVlUls0VJXg7A8u-Ts1XbjhazAkj7I99e8QcYP7DkM", string auth = "tBHItJI5svbpez7KI4CCXg")
        => new { endpoint, keys = new { p256dh, auth } };

    private async Task<List<PushSubscription>> Rows(string endpoint)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DuetoDbContext>();
        return await db.PushSubscriptions.AsNoTracking().Where(s => s.Endpoint == endpoint).ToListAsync();
    }

    [Fact]
    public async Task Vapid_public_key_is_public()
    {
        var response = await factory.CreateClient().GetAsync("/api/push/vapid-public-key");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("test-vapid-public-key", body);
        Assert.DoesNotContain("private", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Subscribing_requires_a_token()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/push/subscriptions", Body(NewEndpoint()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Only_mobile_devices_can_manage_subscriptions()
    {
        var devices = await PairedDevices.CreateAsync(factory);

        var subscribe = await devices.Desktop.PostAsJsonAsync("/api/push/subscriptions", Body(NewEndpoint()));
        var unsubscribe = await devices.Desktop.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/push/subscriptions")
        {
            Content = JsonContent.Create(new { endpoint = NewEndpoint() }),
        });

        Assert.Equal(HttpStatusCode.Forbidden, subscribe.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, unsubscribe.StatusCode);
    }

    [Fact]
    public async Task Mobile_can_subscribe_and_it_is_stored()
    {
        var devices = await PairedDevices.CreateAsync(factory);
        var endpoint = NewEndpoint();

        var response = await devices.Mobile.PostAsJsonAsync("/api/push/subscriptions", Body(endpoint));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var row = Assert.Single(await Rows(endpoint));
        Assert.Equal(devices.MobileId, row.DeviceId);
    }

    [Fact]
    public async Task Subscribing_twice_with_the_same_endpoint_keeps_a_single_row_and_updates_the_keys()
    {
        var devices = await PairedDevices.CreateAsync(factory);
        var endpoint = NewEndpoint();

        await devices.Mobile.PostAsJsonAsync("/api/push/subscriptions", Body(endpoint, auth: "firstAuthKey"));
        await devices.Mobile.PostAsJsonAsync("/api/push/subscriptions", Body(endpoint, auth: "secondAuthKey"));

        var row = Assert.Single(await Rows(endpoint));
        Assert.Equal("secondAuthKey", row.Auth);
    }

    [Fact]
    public async Task Endpoint_moves_to_the_new_device_when_the_browser_pairs_again()
    {
        var first = await PairedDevices.CreateAsync(factory);
        var second = await PairedDevices.CreateAsync(factory);
        var endpoint = NewEndpoint();

        await first.Mobile.PostAsJsonAsync("/api/push/subscriptions", Body(endpoint));
        await second.Mobile.PostAsJsonAsync("/api/push/subscriptions", Body(endpoint));

        var row = Assert.Single(await Rows(endpoint));
        Assert.Equal(second.MobileId, row.DeviceId);
    }

    [Theory]
    [InlineData("http://web.push.apple.com/abc")]
    [InlineData("https://example.com/push")]
    [InlineData("https://169.254.169.254/latest")]
    [InlineData("not a url")]
    [InlineData("")]
    public async Task Endpoints_outside_the_push_service_allowlist_are_rejected(string endpoint)
    {
        var devices = await PairedDevices.CreateAsync(factory);

        var response = await devices.Mobile.PostAsJsonAsync("/api/push/subscriptions", Body(endpoint));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(await Rows(endpoint));
    }

    [Theory]
    [InlineData("", "validAuth")]
    [InlineData("valid_p256dh", "")]
    [InlineData("not base64 !!", "validAuth")]
    public async Task Invalid_keys_are_rejected(string p256dh, string auth)
    {
        var devices = await PairedDevices.CreateAsync(factory);

        var response = await devices.Mobile.PostAsJsonAsync("/api/push/subscriptions", Body(NewEndpoint(), p256dh, auth));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Missing_keys_are_rejected()
    {
        var devices = await PairedDevices.CreateAsync(factory);

        var response = await devices.Mobile.PostAsJsonAsync("/api/push/subscriptions", new { endpoint = NewEndpoint() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static HttpRequestMessage Delete(string endpoint) =>
        new(HttpMethod.Delete, "/api/push/subscriptions") { Content = JsonContent.Create(new { endpoint }) };

    [Fact]
    public async Task Mobile_can_unsubscribe()
    {
        var devices = await PairedDevices.CreateAsync(factory);
        var endpoint = NewEndpoint();
        await devices.Mobile.PostAsJsonAsync("/api/push/subscriptions", Body(endpoint));

        var response = await devices.Mobile.SendAsync(Delete(endpoint));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await Rows(endpoint));
    }

    [Fact]
    public async Task Unsubscribing_an_unknown_endpoint_is_idempotent()
    {
        var devices = await PairedDevices.CreateAsync(factory);

        var response = await devices.Mobile.SendAsync(Delete(NewEndpoint()));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task A_device_cannot_remove_another_devices_subscription()
    {
        var owner = await PairedDevices.CreateAsync(factory);
        var other = await PairedDevices.CreateAsync(factory);
        var endpoint = NewEndpoint();
        await owner.Mobile.PostAsJsonAsync("/api/push/subscriptions", Body(endpoint));

        await other.Mobile.SendAsync(Delete(endpoint));

        Assert.Single(await Rows(endpoint));
    }
}
