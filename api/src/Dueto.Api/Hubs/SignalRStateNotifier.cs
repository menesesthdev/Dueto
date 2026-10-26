using Dueto.Core.State;
using Microsoft.AspNetCore.SignalR;

namespace Dueto.Api.Hubs;

internal sealed class SignalRStateNotifier(IHubContext<SyncHub> hub) : IStateNotifier
{
    public Task NotifyStateChangedAsync(
        StateChangedMessage message,
        IReadOnlyCollection<Guid> recipientDeviceIds,
        CancellationToken cancellationToken) =>
        hub.Clients
            .Groups(recipientDeviceIds.Select(SyncHub.DeviceGroup).ToList())
            .SendAsync(SyncHub.StateChangedEvent, message, cancellationToken);
}
