namespace Dueto.Core.State;

/// <summary>Entrega o evento <c>StateChanged</c> em tempo real aos dispositivos indicados.</summary>
public interface IStateNotifier
{
    Task NotifyStateChangedAsync(
        StateChangedMessage message,
        IReadOnlyCollection<Guid> recipientDeviceIds,
        CancellationToken cancellationToken);
}
