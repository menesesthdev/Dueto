using Dueto.Api.Authentication;
using Dueto.Core.State;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Dueto.Api.Hubs;

/// <summary>Hub de sincronização em tempo real (<c>/hub/sync</c>). Servidor → cliente: <c>StateChanged</c>.</summary>
[Authorize]
public sealed class SyncHub(IStateService state) : Hub
{
    public const string Path = "/hub/sync";
    public const string StateChangedEvent = "StateChanged";

    /// <summary>Grupo de um dispositivo: recebe os eventos dirigidos a ele.</summary>
    public static string DeviceGroup(Guid deviceId) => $"device:{deviceId}";

    public override async Task OnConnectedAsync()
    {
        var identity = Context.User!.ToDeviceIdentity();
        await Groups.AddToGroupAsync(Context.ConnectionId, DeviceGroup(identity.DeviceId));

        // Snapshot ao conectar: o cliente aplica se a versão for mais nova que a última aplicada.
        var snapshot = await state.GetAsync(identity, Context.ConnectionAborted);
        if (snapshot.WallpaperId is not null)
        {
            await Clients.Caller.SendAsync(
                StateChangedEvent,
                new StateChangedMessage(snapshot.WallpaperId, snapshot.Version, snapshot.OriginDeviceId),
                Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
    }
}
