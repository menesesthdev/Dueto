using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dueto.Core.State;

namespace Dueto.Tests.Infrastructure;

/// <summary>Um par já formado (desktop + mobile), com clientes HTTP autenticados.</summary>
public sealed class PairedDevices
{
    public required HttpClient Desktop { get; init; }
    public required HttpClient Mobile { get; init; }
    public required string DesktopToken { get; init; }
    public required string MobileToken { get; init; }
    public required Guid DesktopId { get; init; }
    public required Guid MobileId { get; init; }

    public static async Task<PairedDevices> CreateAsync(PostgresApiFactory factory)
    {
        var anonymous = factory.CreateClient();

        var start = await anonymous.PostAsJsonAsync("/api/pairing/start", new { deviceName = "PC", kind = "desktop" });
        start.EnsureSuccessStatusCode();
        var started = (await start.Content.ReadFromJsonAsync<StartBody>())!;

        var complete = await anonymous.PostAsJsonAsync(
            "/api/pairing/complete",
            new { code = started.Code, deviceName = "iPhone", kind = "mobile" });
        complete.EnsureSuccessStatusCode();
        var completed = (await complete.Content.ReadFromJsonAsync<CompleteBody>())!;

        var desktop = Authenticated(factory, started.DeviceToken);
        var mobile = Authenticated(factory, completed.DeviceToken);

        var state = (await desktop.GetFromJsonAsync<StateSnapshot>("/api/state"))!;

        return new PairedDevices
        {
            Desktop = desktop,
            Mobile = mobile,
            DesktopToken = started.DeviceToken,
            MobileToken = completed.DeviceToken,
            DesktopId = state.Applied.Single(a => a.Kind == "desktop").DeviceId,
            MobileId = state.Applied.Single(a => a.Kind == "mobile").DeviceId,
        };
    }

    public static HttpClient Authenticated(PostgresApiFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public sealed record StartBody(string DeviceToken, string Code, DateTimeOffset ExpiresAt);

    public sealed record CompleteBody(string DeviceToken);
}
