namespace Dueto.Core.Pairing;

public interface IDeviceAuthenticator
{
    /// <summary>Resolve o dispositivo dono do token; nulo se o token não existir ou foi revogado.</summary>
    Task<DeviceIdentity?> AuthenticateAsync(string token, CancellationToken cancellationToken);
}
