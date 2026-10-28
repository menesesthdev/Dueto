using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Dueto.Core.Push;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dueto.Infrastructure.Push;

/// <summary>
/// Envio real via Web Push no formato moderno: payload <c>aes128gcm</c> (RFC 8291) e identificação
/// <c>vapid</c> (RFC 8292). É o formato que o push service da Apple (iPhone) usa.
/// </summary>
internal sealed class WebPushSender : IPushSender, IDisposable
{
    // Uma troca de wallpaper velha não vale a pena ser entregue: depois de 1 h o lembrete seguinte já a substituiu.
    private const int TimeToLiveSeconds = 3_600;
    private const int MaxReasonBytes = 1_024;

    private readonly ILogger<WebPushSender> _logger;
    private readonly VapidSigner? _signer;
    private readonly HttpClient _httpClient;

    public WebPushSender(IOptions<PushOptions> options, TimeProvider timeProvider, ILogger<WebPushSender> logger)
    {
        _logger = logger;

        // Sem seguir redirecionamentos: um push service não deveria redirecionar, e seguir um redirect
        // seria outra porta para o servidor chamar hosts que ninguém autorizou.
        _httpClient = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(10),
        };

        var settings = options.Value;
        if (settings.IsConfigured)
        {
            _signer = new VapidSigner(settings.VapidPublicKey!, settings.VapidPrivateKey!, settings.VapidSubject!, timeProvider);
        }
    }

    public bool IsEnabled => _signer is not null;

    public string? VapidPublicKey => _signer?.PublicKey;

    public async Task<PushSendStatus> SendAsync(
        PushTarget target,
        PushPayload payload,
        CancellationToken cancellationToken)
    {
        if (_signer is null)
        {
            return PushSendStatus.Failed;
        }

        try
        {
            var body = Aes128GcmEncryption.Encrypt(
                JsonSerializer.SerializeToUtf8Bytes(payload, JsonSerializerOptions.Web),
                Base64Url.DecodeFromChars(target.P256dh),
                Base64Url.DecodeFromChars(target.Auth));

            var endpoint = new Uri(target.Endpoint);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new ByteArrayContent(body) };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Content.Headers.ContentEncoding.Add("aes128gcm");
            request.Headers.TryAddWithoutValidation("Authorization", _signer.AuthorizationHeader(endpoint.GetLeftPart(UriPartial.Authority)));
            request.Headers.TryAddWithoutValidation("TTL", TimeToLiveSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            request.Headers.TryAddWithoutValidation("Urgency", "high");

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return PushSendStatus.Sent;
            }

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                return PushSendStatus.SubscriptionGone;
            }

            // Só o status e o "reason" do push service (ex.: BadJwtToken) vão para o log:
            // endpoint, chaves da assinatura e chaves VAPID nunca.
            _logger.LogWarning(
                "Web Push delivery failed (status {Status}, reason {Reason}).",
                (int)response.StatusCode,
                await ReadReasonAsync(response, cancellationToken));
            return PushSendStatus.Failed;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
            or FormatException or ArgumentException or System.Security.Cryptography.CryptographicException
            && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Web Push delivery failed ({ErrorType}).", exception.GetType().Name);
            return PushSendStatus.Failed;
        }
    }

    private static async Task<string> ReadReasonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[MaxReasonBytes];
            var read = await stream.ReadAsync(buffer, cancellationToken);

            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(buffer, 0, read));
            var reason = document.RootElement.TryGetProperty("reason", out var value) ? value.GetString() : null;

            // Só aceita um identificador curto e simples, para o log não virar canal de conteúdo arbitrário.
            return reason is { Length: > 0 and <= 64 } && reason.All(char.IsAsciiLetterOrDigit) ? reason : "unknown";
        }
        catch (Exception exception) when (exception is JsonException or IOException or InvalidOperationException)
        {
            return "unknown";
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _signer?.Dispose();
    }
}
