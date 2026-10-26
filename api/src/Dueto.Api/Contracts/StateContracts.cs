namespace Dueto.Api.Contracts;

public sealed record SetStateRequest(string? WallpaperId);

public sealed record SetStateResponse(long Version);

public sealed record AppliedRequest(string? WallpaperId, long Version);
