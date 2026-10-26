using Dueto.Core.Pairing;

namespace Dueto.Core.State;

public interface IStateService
{
    Task<StateSnapshot> GetAsync(DeviceIdentity identity, CancellationToken cancellationToken);

    /// <summary>Define o wallpaper do par e incrementa a versão de forma atômica.</summary>
    Task<SetStateResult> SetAsync(DeviceIdentity identity, string wallpaperId, CancellationToken cancellationToken);

    /// <summary>Confirma que o dispositivo aplicou (wallpaperId, version). Idempotente.</summary>
    Task<ApplyStatus> ApplyAsync(
        DeviceIdentity identity,
        string wallpaperId,
        long version,
        CancellationToken cancellationToken);
}
