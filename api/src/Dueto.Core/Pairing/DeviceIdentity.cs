using Dueto.Core.Domain;

namespace Dueto.Core.Pairing;

/// <summary>Quem está fazendo a requisição: o dispositivo autenticado e o par a que pertence.</summary>
public sealed record DeviceIdentity(Guid DeviceId, Guid PairId, DeviceKind Kind);
