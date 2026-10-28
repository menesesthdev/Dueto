using System.Net;
using System.Net.Http.Json;
using Dueto.Core.Domain;
using Dueto.Core.State;
using Dueto.Infrastructure.Persistence;
using Dueto.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dueto.Tests.Api;

/// <summary>Wallpaper animado só existe no desktop (CLAUDE.md §1). "drift-loop" é o animado do catálogo de teste.</summary>
[Collection(PostgresCollection.Name)]
public class AnimatedWallpaperTests(PostgresApiFactory factory) : IDisposable
{
    private const string Animated = "drift-loop";

    public void Dispose() => factory.PushSender.Reset();

    private static Task<HttpResponseMessage> Set(HttpClient client, string wallpaperId) =>
        client.PutAsJsonAsync("/api/state", new { wallpaperId });

    private static async Task<StateSnapshot> GetState(HttpClient client) =>
        (await client.GetFromJsonAsync<StateSnapshot>("/api/state"))!;

    private async Task<List<Reminder>> RemindersOf(Guid deviceId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DuetoDbContext>();
        return await db.Reminders.AsNoTracking().Where(r => r.DeviceId == deviceId).OrderBy(r => r.Version).ThenBy(r => r.Sequence).ToListAsync();
    }

    [Fact]
    public async Task The_mobile_cannot_set_an_animated_wallpaper()
    {
        var devices = await PairedDevices.CreateAsync(factory);

        var response = await Set(devices.Mobile, Animated);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0, (await GetState(devices.Desktop)).Version); // nada mudou
    }

    [Fact]
    public async Task The_desktop_can_set_an_animated_wallpaper()
    {
        var devices = await PairedDevices.CreateAsync(factory);

        var response = await Set(devices.Desktop, Animated);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await GetState(devices.Desktop);
        Assert.Equal(Animated, state.WallpaperId);
        Assert.Equal(1, state.Version);
        Assert.Equal(1, state.Applied.Single(a => a.Kind == "desktop").Version); // o desktop já fica como aplicado
    }

    [Fact]
    public async Task An_animated_change_from_the_desktop_sends_no_push_and_creates_no_reminders()
    {
        var devices = await PairedDevices.CreateAsync(factory);
        var endpoint = PushSubscriptionEndpointTests.NewEndpoint();
        await devices.Mobile.PostAsJsonAsync("/api/push/subscriptions", PushSubscriptionEndpointTests.Body(endpoint));

        await Set(devices.Desktop, Animated);

        Assert.Empty(factory.PushSender.SentTo(endpoint));
        Assert.Empty(await RemindersOf(devices.MobileId));
    }

    [Fact]
    public async Task An_animated_change_cancels_the_reminders_of_the_previous_static_wallpaper()
    {
        var devices = await PairedDevices.CreateAsync(factory);
        await Set(devices.Desktop, "neon-tide");
        Assert.All(await RemindersOf(devices.MobileId), r => Assert.Equal(ReminderStatus.Pending, r.Status));

        await Set(devices.Desktop, Animated);

        var reminders = await RemindersOf(devices.MobileId);
        Assert.Equal(3, reminders.Count); // nenhum novo
        Assert.All(reminders, r => Assert.Equal(ReminderStatus.Cancelled, r.Status)); // os antigos ficaram obsoletos
    }

    [Fact]
    public async Task The_mobile_cannot_confirm_an_animated_wallpaper()
    {
        var devices = await PairedDevices.CreateAsync(factory);
        await Set(devices.Desktop, Animated);

        var response = await devices.Mobile.PostAsJsonAsync("/api/state/applied", new { wallpaperId = Animated, version = 1 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Null((await GetState(devices.Mobile)).Applied.Single(a => a.Kind == "mobile").Version);
    }

    [Fact]
    public async Task The_desktop_can_confirm_an_animated_wallpaper()
    {
        var devices = await PairedDevices.CreateAsync(factory);
        await Set(devices.Desktop, Animated);

        var response = await devices.Desktop.PostAsJsonAsync("/api/state/applied", new { wallpaperId = Animated, version = 1 });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task The_mobile_can_still_set_a_static_wallpaper_after_an_animated_one()
    {
        var devices = await PairedDevices.CreateAsync(factory);
        await Set(devices.Desktop, Animated);

        var response = await Set(devices.Mobile, "neon-tide");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, (await GetState(devices.Desktop)).Version);
    }

    [Fact]
    public async Task The_hub_still_tells_the_mobile_about_an_animated_change_so_its_view_of_the_state_stays_true()
    {
        var devices = await PairedDevices.CreateAsync(factory);
        await using var mobile = new SyncHubTests.HubClient(factory, devices.MobileToken);
        await mobile.StartAsync();

        await Set(devices.Desktop, Animated);
        var message = await mobile.NextAsync();

        // O celular é informado do estado verdadeiro; decidir não aplicar um animado é do PWA, que consulta o catálogo.
        Assert.Equal(new StateChangedMessage(Animated, 1, devices.DesktopId), message);
    }

    [Fact]
    public async Task A_static_change_from_the_desktop_still_notifies_as_before()
    {
        var devices = await PairedDevices.CreateAsync(factory);
        var endpoint = PushSubscriptionEndpointTests.NewEndpoint();
        await devices.Mobile.PostAsJsonAsync("/api/push/subscriptions", PushSubscriptionEndpointTests.Body(endpoint));

        await Set(devices.Desktop, "neon-tide");

        Assert.Single(factory.PushSender.SentTo(endpoint));
        Assert.Equal(3, (await RemindersOf(devices.MobileId)).Count);
    }
}
