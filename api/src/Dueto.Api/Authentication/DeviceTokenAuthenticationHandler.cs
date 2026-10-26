using System.Text.Encodings.Web;
using Dueto.Core.Pairing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Dueto.Api.Authentication;

/// <summary>
/// Autentica pelo token do dispositivo: <c>Authorization: Bearer &lt;token&gt;</c>.
/// Nas conexões WebSocket do hub o navegador não envia cabeçalhos, então nessa rota
/// (e só nela) o token também é aceito em <c>access_token</c> na query string.
/// </summary>
internal sealed class DeviceTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IDeviceAuthenticator authenticator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "DeviceToken";
    public const string HubPathPrefix = "/hub";

    private const string BearerPrefix = "Bearer ";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ReadToken();
        if (token is null)
        {
            return AuthenticateResult.NoResult();
        }

        var identity = await authenticator.AuthenticateAsync(token, Context.RequestAborted);

        // A mensagem de falha nunca inclui o token.
        return identity is null
            ? AuthenticateResult.Fail("Invalid device token.")
            : AuthenticateResult.Success(new AuthenticationTicket(
                DeviceClaims.ToPrincipal(identity, SchemeName),
                SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer";
        return Task.CompletedTask;
    }

    private string? ReadToken()
    {
        var header = Request.Headers.Authorization.ToString();
        if (header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return header[BearerPrefix.Length..].Trim();
        }

        if (Request.Path.StartsWithSegments(HubPathPrefix))
        {
            var queryToken = Request.Query["access_token"].ToString();
            return string.IsNullOrEmpty(queryToken) ? null : queryToken;
        }

        return null;
    }
}
