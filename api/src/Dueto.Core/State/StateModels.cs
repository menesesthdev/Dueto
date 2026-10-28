namespace Dueto.Core.State;

/// <summary>O que um dispositivo confirmou ter aplicado. Campos nulos se ainda não confirmou nada.</summary>
public sealed record DeviceAppliedDto(
    Guid DeviceId,
    string Kind,
    string? WallpaperId,
    long? Version,
    DateTimeOffset? AppliedAt);

/// <summary>Estado atual do par. <c>Version</c> 0 e <c>WallpaperId</c> nulo: nenhuma troca ainda.</summary>
public sealed record StateSnapshot(
    string? WallpaperId,
    long Version,
    Guid? OriginDeviceId,
    IReadOnlyList<DeviceAppliedDto> Applied);

/// <summary>Evento <c>StateChanged</c> enviado pelo hub.</summary>
public sealed record StateChangedMessage(string WallpaperId, long Version, Guid? OriginDeviceId);

public enum SetStateStatus
{
    Success,
    WallpaperNotFound,

    /// <summary>Wallpaper animado só existe no desktop: o celular não pode defini-lo.</summary>
    AnimatedNotAllowedOnMobile,
}

public sealed record SetStateResult(SetStateStatus Status, long Version = 0);

public enum ApplyStatus
{
    Success,
    WallpaperNotFound,

    /// <summary>Versão menor que 1 ou maior que a versão atual do par.</summary>
    InvalidVersion,

    /// <summary>A versão atual do par aponta para outro wallpaper.</summary>
    Mismatch,

    /// <summary>Wallpaper animado só existe no desktop: o celular não pode confirmá-lo.</summary>
    AnimatedNotAllowedOnMobile,
}
