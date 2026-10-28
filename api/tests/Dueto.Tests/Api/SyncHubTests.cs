using System.Collections.Concurrent;
using System.Net.Http.Json;
using Dueto.Core.State;
using Dueto.Tests.Infrastructure;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Dueto.Tests.Api;

[Collection(PostgresCollection.Name)]
public class SyncHubTests(PostgresApiFactory factory)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>Conexão de teste que guarda os eventos StateChanged recebidos.</summary>
    internal sealed class HubClient : IAsyncDisposable
    {
        public HubConnection Connection { get; }
        public ConcurrentQueue<StateChangedMessage> Received { get; } = new();
        private readonly SemaphoreSlim _signal = new(0);

        public HubClient(PostgresApiFactory factory, string? token)
        {
            Connection = new HubConnectionBuilder()
                .WithUrl("http://localhost/hub/sync", options =>
                {
                    options.Transports = HttpTransportType.LongPolling;
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                    if (token is not null)
                    {
                        options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                    }
                })
                .Build();

            Connection.On<StateChangedMessage>("StateChanged", message =>
            {
                Received.Enqueue(message);
                _signal.Release();
            });
        }

        public Task StartAsync() => Connection.StartAsync();

        public async Task<StateChangedMessage> NextAsync()
        {
            Assert.True(await _signal.WaitAsync(Timeout), "Timed out waiting for StateChanged.");
            Received.TryDequeue(out var message);
            return message!;
        }

        public ValueTask DisposeAsync() => Connection.DisposeAsync();
    }

    [Fact]
    public async Task Connecting_without_a_token_is_rejected()
    {
        await using var client = new HubClient(factory, token: null);

        await Assert.ThrowsAnyAsync<Exception>(client.StartAsync);
    }

    [Fact]
    public async Task Connecting_with_an_invalid_token_is_rejected()
    {
        await using var client = new HubClient(factory, token: "not-a-real-token");

        await Assert.ThrowsAnyAsync<Exception>(client.StartAsync);
    }

    [Fact]
    public async Task Change_from_desktop_reaches_the_mobile_and_carries_the_origin()
    {
        var devices = await PairedDevices.CreateAsync(factory);
        await using var mobile = new HubClient(factory, devices.MobileToken);
        await mobile.StartAsync();

        await devices.Desktop.PutAsJsonAsync("/api/state", new { wallpaperId = "neon-tide" });
        var message = await mobile.NextAsync();

        Assert.Equal(new StateChangedMessage("neon-tide", 1, devices.DesktopId), message);
    }

    [Fact]
    public async Task Change_from_mobile_reaches_the_desktop()
    {
        var devices = await PairedDevices.CreateAsync(factory);
        await using var desktop = new HubClient(factory, devices.DesktopToken);
        await desktop.StartAsync();

        await devices.Mobile.PutAsJsonAsync("/api/state", new { wallpaperId = "aurora-dunes" });
        var message = await desktop.NextAsync();

        Assert.Equal(new StateChangedMessage("aurora-dunes", 1, devices.MobileId), message);
    }

    [Fact]
    public async Task Origin_device_does_not_receive_its_own_event()
    {
        var devices = await PairedDevices.CreateAsync(factory);
        await using var desktop = new HubClient(factory, devices.DesktopToken);
        await using var mobile = new HubClient(factory, devices.MobileToken);
        await desktop.StartAsync();
        await mobile.StartAsync();

        await devices.Desktop.PutAsJsonAsync("/api/state", new { wallpaperId = "neon-tide" });
        await mobile.NextAsync(); // a entrega ao mobile já aconteceu: a do desktop também teria acontecido

        Assert.Empty(desktop.Received);
    }

    [Fact]
    public async Task Connecting_sends_a_snapshot_of_the_current_state()
    {
        var devices = await PairedDevices.CreateAsync(factory);
        await devices.Desktop.PutAsJsonAsync("/api/state", new { wallpaperId = "aurora-dunes" });
        await devices.Desktop.PutAsJsonAsync("/api/state", new { wallpaperId = "neon-tide" });

        await using var mobile = new HubClient(factory, devices.MobileToken);
        await mobile.StartAsync();
        var snapshot = await mobile.NextAsync();

        Assert.Equal(new StateChangedMessage("neon-tide", 2, devices.DesktopId), snapshot);
    }

    [Fact]
    public async Task Connecting_to_a_pair_with_no_wallpaper_sends_no_snapshot()
    {
        var devices = await PairedDevices.CreateAsync(factory);
        await using var mobile = new HubClient(factory, devices.MobileToken);
        await mobile.StartAsync();

        await Task.Delay(300);

        Assert.Empty(mobile.Received);
    }

    [Fact]
    public async Task Events_do_not_leak_to_other_pairs()
    {
        var first = await PairedDevices.CreateAsync(factory);
        var second = await PairedDevices.CreateAsync(factory);
        await using var firstMobile = new HubClient(factory, first.MobileToken);
        await using var secondMobile = new HubClient(factory, second.MobileToken);
        await firstMobile.StartAsync();
        await secondMobile.StartAsync();

        await first.Desktop.PutAsJsonAsync("/api/state", new { wallpaperId = "neon-tide" });
        await firstMobile.NextAsync();

        Assert.Empty(secondMobile.Received);
    }
}
