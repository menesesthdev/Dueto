using System.Net;
using System.Net.Http.Json;
using Dueto.Core.Domain;
using Dueto.Core.Push;
using Dueto.Infrastructure.Persistence;
using Dueto.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dueto.Tests.Api;

/// <summary>
/// Regras de push e lembretes (CLAUDE.md §9, itens 5 a 8). O envio real é trocado por um falso e o tempo
/// é controlado por um relógio de teste; o processador roda à mão, sem esperar o agendador de fundo.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ReminderFlowTests(PostgresApiFactory factory) : IDisposable
{
    private static readonly TimeSpan[] Delays = [TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(60)];

    public void Dispose() => factory.PushSender.Reset();

    private async Task<(PairedDevices Devices, string Endpoint)> PairedWithSubscribedMobile()
    {
        var devices = await PairedDevices.CreateAsync(factory);
        var endpoint = PushSubscriptionEndpointTests.NewEndpoint();
        var response = await devices.Mobile.PostAsJsonAsync("/api/push/subscriptions", PushSubscriptionEndpointTests.Body(endpoint));
        response.EnsureSuccessStatusCode();
        return (devices, endpoint);
    }

    private static Task<HttpResponseMessage> Set(HttpClient client, string wallpaperId) =>
        client.PutAsJsonAsync("/api/state", new { wallpaperId });

    private async Task<List<Reminder>> RemindersOf(Guid deviceId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DuetoDbContext>();
        return await db.Reminders.AsNoTracking()
            .Where(r => r.DeviceId == deviceId)
            .OrderBy(r => r.Version).ThenBy(r => r.Sequence)
            .ToListAsync();
    }

    private async Task ProcessDue()
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IReminderProcessor>().ProcessDueAsync(CancellationToken.None);
    }

    private static void AssertAll(IEnumerable<Reminder> reminders, ReminderStatus status) =>
        Assert.All(reminders, r => Assert.Equal(status, r.Status));

    [Fact]
    public async Task Change_from_desktop_pushes_to_the_mobile_and_creates_three_reminders()
    {
        var (devices, endpoint) = await PairedWithSubscribedMobile();
        var start = factory.Clock.GetUtcNow();

        await Set(devices.Desktop, "neon-tide");

        Assert.Equal([new PushPayload("neon-tide", 1)], factory.PushSender.SentTo(endpoint));

        var reminders = await RemindersOf(devices.MobileId);
        Assert.Equal([1, 2, 3], reminders.Select(r => r.Sequence));

        // O Postgres guarda microssegundos e o .NET ticks de 100 ns: compara com tolerância de 1 ms.
        for (var i = 0; i < Delays.Length; i++)
        {
            Assert.InRange((reminders[i].DueAt - (start + Delays[i])).Duration(), TimeSpan.Zero, TimeSpan.FromMilliseconds(1));
        }

        Assert.All(reminders, r => Assert.Equal(1, r.Version));
        AssertAll(reminders, ReminderStatus.Pending);
        Assert.Empty(await RemindersOf(devices.DesktopId));
    }

    [Fact]
    public async Task Change_from_mobile_creates_no_push_and_no_reminders()
    {
        var (devices, endpoint) = await PairedWithSubscribedMobile();

        await Set(devices.Mobile, "neon-tide");

        Assert.Empty(factory.PushSender.SentTo(endpoint));
        Assert.Empty(await RemindersOf(devices.MobileId));
        Assert.Empty(await RemindersOf(devices.DesktopId));
    }

    [Fact]
    public async Task Nothing_is_created_when_push_is_not_configured()
    {
        var (devices, endpoint) = await PairedWithSubscribedMobile();
        factory.PushSender.IsEnabled = false;

        var response = await Set(devices.Desktop, "neon-tide");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(factory.PushSender.SentTo(endpoint));
        Assert.Empty(await RemindersOf(devices.MobileId));
    }

    [Fact]
    public async Task Reminders_fire_at_their_due_time_and_only_once()
    {
        var (devices, endpoint) = await PairedWithSubscribedMobile();
        await Set(devices.Desktop, "neon-tide");
        Assert.Single(factory.PushSender.SentTo(endpoint)); // o push imediato

        await ProcessDue();
        Assert.Single(factory.PushSender.SentTo(endpoint)); // nada venceu ainda

        factory.Clock.Advance(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(1));
        await ProcessDue();
        await ProcessDue(); // rodar de novo não reenvia
        Assert.Equal(2, factory.PushSender.SentTo(endpoint).Count);

        factory.Clock.Advance(TimeSpan.FromMinutes(13));
        await ProcessDue();
        Assert.Equal(3, factory.PushSender.SentTo(endpoint).Count);

        factory.Clock.Advance(TimeSpan.FromMinutes(45));
        await ProcessDue();
        Assert.Equal(4, factory.PushSender.SentTo(endpoint).Count);

        AssertAll(await RemindersOf(devices.MobileId), ReminderStatus.Sent);
        Assert.All(factory.PushSender.SentTo(endpoint), p => Assert.Equal(new PushPayload("neon-tide", 1), p));
    }

    [Fact]
    public async Task Overdue_reminders_still_fire_after_a_long_outage()
    {
        var (devices, endpoint) = await PairedWithSubscribedMobile();
        await Set(devices.Desktop, "neon-tide");

        // Serviço "parado" por 3 horas: os lembretes vivem no banco e disparam na primeira varredura.
        factory.Clock.Advance(TimeSpan.FromHours(3));
        await ProcessDue();

        Assert.Equal(4, factory.PushSender.SentTo(endpoint).Count);
    }

    [Fact]
    public async Task A_new_version_cancels_the_pending_reminders_of_older_versions()
    {
        var (devices, endpoint) = await PairedWithSubscribedMobile();
        await Set(devices.Desktop, "aurora-dunes");
        await Set(devices.Desktop, "neon-tide");

        var reminders = await RemindersOf(devices.MobileId);
        AssertAll(reminders.Where(r => r.Version == 1), ReminderStatus.Cancelled);
        AssertAll(reminders.Where(r => r.Version == 2), ReminderStatus.Pending);

        factory.Clock.Advance(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(1));
        await ProcessDue();

        var sent = factory.PushSender.SentTo(endpoint);
        Assert.Equal(1, sent.Count(p => p.Version == 1)); // só o push imediato da versão antiga
        Assert.Equal(2, sent.Count(p => p.Version == 2)); // imediato + 1º lembrete
    }

    [Fact]
    public async Task Confirming_cancels_the_remaining_reminders()
    {
        var (devices, endpoint) = await PairedWithSubscribedMobile();
        await Set(devices.Desktop, "neon-tide");

        var applied = await devices.Mobile.PostAsJsonAsync("/api/state/applied", new { wallpaperId = "neon-tide", version = 1 });
        Assert.Equal(HttpStatusCode.NoContent, applied.StatusCode);

        AssertAll(await RemindersOf(devices.MobileId), ReminderStatus.Cancelled);

        factory.Clock.Advance(TimeSpan.FromHours(2));
        await ProcessDue();
        Assert.Single(factory.PushSender.SentTo(endpoint)); // só o push imediato
    }

    [Fact]
    public async Task Confirming_after_the_first_reminder_cancels_only_the_rest()
    {
        var (devices, endpoint) = await PairedWithSubscribedMobile();
        await Set(devices.Desktop, "neon-tide");
        factory.Clock.Advance(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(1));
        await ProcessDue();

        await devices.Mobile.PostAsJsonAsync("/api/state/applied", new { wallpaperId = "neon-tide", version = 1 });

        var reminders = await RemindersOf(devices.MobileId);
        Assert.Equal(
            [ReminderStatus.Sent, ReminderStatus.Cancelled, ReminderStatus.Cancelled],
            reminders.Select(r => r.Status));

        factory.Clock.Advance(TimeSpan.FromHours(2));
        await ProcessDue();
        Assert.Equal(2, factory.PushSender.SentTo(endpoint).Count);
    }

    [Fact]
    public async Task Confirming_an_older_version_keeps_the_newer_reminders()
    {
        var (devices, _) = await PairedWithSubscribedMobile();
        await Set(devices.Desktop, "aurora-dunes");
        await Set(devices.Desktop, "neon-tide");

        await devices.Mobile.PostAsJsonAsync("/api/state/applied", new { wallpaperId = "aurora-dunes", version = 1 });

        AssertAll((await RemindersOf(devices.MobileId)).Where(r => r.Version == 2), ReminderStatus.Pending);
    }

    [Fact]
    public async Task A_stale_reminder_is_never_sent()
    {
        var (devices, endpoint) = await PairedWithSubscribedMobile();
        await Set(devices.Desktop, "aurora-dunes");
        await Set(devices.Desktop, "neon-tide");

        // Defesa em profundidade: mesmo que um lembrete velho volte a "pendente", a versão não é mais a atual.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DuetoDbContext>();
            await db.Reminders.Where(r => r.DeviceId == devices.MobileId && r.Version == 1)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, ReminderStatus.Pending));
        }

        factory.Clock.Advance(TimeSpan.FromHours(2));
        await ProcessDue();

        Assert.Equal(1, factory.PushSender.SentTo(endpoint).Count(p => p.Version == 1));
    }

    [Fact]
    public async Task A_dead_subscription_is_removed_and_the_others_still_receive()
    {
        var (devices, deadEndpoint) = await PairedWithSubscribedMobile();
        var liveEndpoint = PushSubscriptionEndpointTests.NewEndpoint();
        await devices.Mobile.PostAsJsonAsync("/api/push/subscriptions", PushSubscriptionEndpointTests.Body(liveEndpoint));
        factory.PushSender.Responder = target =>
            target.Endpoint == deadEndpoint ? PushSendStatus.SubscriptionGone : PushSendStatus.Sent;

        await Set(devices.Desktop, "neon-tide");

        Assert.Single(factory.PushSender.SentTo(liveEndpoint));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DuetoDbContext>();
        Assert.False(await db.PushSubscriptions.AnyAsync(s => s.Endpoint == deadEndpoint));
        Assert.True(await db.PushSubscriptions.AnyAsync(s => s.Endpoint == liveEndpoint));
    }

    [Fact]
    public async Task A_transient_failure_keeps_the_subscription()
    {
        var (devices, endpoint) = await PairedWithSubscribedMobile();
        factory.PushSender.Responder = _ => PushSendStatus.Failed;

        await Set(devices.Desktop, "neon-tide");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DuetoDbContext>();
        Assert.True(await db.PushSubscriptions.AnyAsync(s => s.Endpoint == endpoint));
    }

    [Fact]
    public async Task A_crashing_push_sender_does_not_break_the_state_change()
    {
        var (devices, _) = await PairedWithSubscribedMobile();
        factory.PushSender.Responder = _ => throw new InvalidOperationException("push service exploded");

        var response = await Set(devices.Desktop, "neon-tide");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, (await devices.Desktop.GetFromJsonAsync<Core.State.StateSnapshot>("/api/state"))!.Version);
        Assert.Equal(3, (await RemindersOf(devices.MobileId)).Count);
    }

    [Fact]
    public async Task A_mobile_without_subscription_still_gets_reminders_ready_for_when_it_subscribes()
    {
        var devices = await PairedDevices.CreateAsync(factory);

        await Set(devices.Desktop, "neon-tide");

        Assert.Equal(3, (await RemindersOf(devices.MobileId)).Count);
    }
}
