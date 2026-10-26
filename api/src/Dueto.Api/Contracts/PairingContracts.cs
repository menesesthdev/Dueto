namespace Dueto.Api.Contracts;

public sealed record StartPairingRequest(string? DeviceName, string? Kind);

public sealed record StartPairingResponse(string DeviceToken, string Code, DateTimeOffset ExpiresAt);

public sealed record CompletePairingRequest(string? Code, string? DeviceName, string? Kind);

public sealed record CompletePairingResponse(string DeviceToken);
