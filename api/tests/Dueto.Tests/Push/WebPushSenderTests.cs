using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dueto.Core.Push;
using Dueto.Infrastructure.Push;
using Dueto.Tests.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dueto.Tests.Push;

/// <summary>
/// Testa o remetente REAL contra um "push service" local, sem internet. O teste faz o papel do navegador:
/// decifra o corpo com a chave privada da assinatura e confere o JWT VAPID, o TTL e os códigos de resposta.
/// </summary>
public sealed class WebPushSenderTests : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly List<string> _logs = [];
    private readonly ECDiffieHellman _browserKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
    private readonly byte[] _authSecret = RandomNumberGenerator.GetBytes(16);
    private readonly VapidTestKeys _vapid = VapidTestKeys.Generate();
    private readonly PushTarget _target;
    private readonly PushOptions _options;
    private readonly TestClock _clock = new();

    private int _statusToReturn = 201;
    private string _bodyToReturn = "";
    private HttpListenerRequest? _lastRequest;
    private byte[] _lastBody = [];

    public WebPushSenderTests()
    {
        var port = GetFreePort();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Start();
        _ = Task.Run(Serve);

        _options = new PushOptions
        {
            VapidPublicKey = _vapid.PublicKey,
            VapidPrivateKey = _vapid.PrivateKey,
            VapidSubject = "mailto:owner@example.com",
        };

        _target = new PushTarget(
            $"http://localhost:{port}/push/abc123",
            Base64Url.EncodeToString(Aes128GcmEncryption.ExportUncompressedPoint(_browserKey)),
            Base64Url.EncodeToString(_authSecret));
    }

    private static int GetFreePort()
    {
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private async Task Serve()
    {
        while (_listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _lastRequest = context.Request;
                using var buffer = new MemoryStream();
                await context.Request.InputStream.CopyToAsync(buffer);
                _lastBody = buffer.ToArray();

                context.Response.StatusCode = _statusToReturn;
                var reply = Encoding.UTF8.GetBytes(_bodyToReturn);
                await context.Response.OutputStream.WriteAsync(reply);
                context.Response.Close();
            }
            catch (Exception) when (!_listener.IsListening)
            {
                return;
            }
        }
    }

    private WebPushSender CreateSender(PushOptions? options = null) =>
        new(Options.Create(options ?? _options), _clock, new ListLogger<WebPushSender>(_logs));

    /// <summary>Lado do navegador (RFC 8291, seção 3.4): decifra o registro com a chave privada da assinatura.</summary>
    private byte[] DecryptAsBrowser(byte[] body)
    {
        var salt = body[..16];
        var serverPublicKey = body[21..86];
        var record = body[86..];

        using var serverKey = Aes128GcmEncryption.ImportPublicKey(serverPublicKey);
        var ecdhSecret = _browserKey.DeriveRawSecretAgreement(serverKey.PublicKey);
        var browserPublicKey = Aes128GcmEncryption.ExportUncompressedPoint(_browserKey);

        var keyInfo = Encoding.ASCII.GetBytes("WebPush: info\0").Concat(browserPublicKey).Concat(serverPublicKey).ToArray();
        var ikm = HKDF.Expand(HashAlgorithmName.SHA256, HKDF.Extract(HashAlgorithmName.SHA256, ecdhSecret, _authSecret), 32, keyInfo);
        var prk = HKDF.Extract(HashAlgorithmName.SHA256, ikm, salt);
        var key = HKDF.Expand(HashAlgorithmName.SHA256, prk, 16, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
        var nonce = HKDF.Expand(HashAlgorithmName.SHA256, prk, 12, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));

        var plaintext = new byte[record.Length - 16];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, record[..^16], record[^16..], plaintext);

        Assert.Equal(0x02, plaintext[^1]); // delimitador do último registro
        return plaintext[..^1];
    }

    [Fact]
    public async Task Sends_an_encrypted_payload_that_the_browser_can_decrypt()
    {
        using var sender = CreateSender();

        var status = await sender.SendAsync(_target, new PushPayload("neon-tide", 7), CancellationToken.None);

        Assert.Equal(PushSendStatus.Sent, status);
        Assert.Equal("aes128gcm", _lastRequest!.Headers["Content-Encoding"]);
        Assert.Equal("application/octet-stream", _lastRequest.ContentType);
        Assert.DoesNotContain("neon-tide", Encoding.UTF8.GetString(_lastBody)); // não vai em claro

        var json = Encoding.UTF8.GetString(DecryptAsBrowser(_lastBody));
        Assert.Equal("""{"wallpaperId":"neon-tide","version":7}""", json);
    }

    [Fact]
    public async Task Sends_ttl_and_urgency()
    {
        using var sender = CreateSender();

        await sender.SendAsync(_target, new PushPayload("x", 1), CancellationToken.None);

        Assert.Equal("3600", _lastRequest!.Headers["TTL"]);
        Assert.Equal("high", _lastRequest.Headers["Urgency"]);
    }

    [Fact]
    public async Task Authorization_uses_the_vapid_scheme_with_a_valid_signed_jwt()
    {
        using var sender = CreateSender();

        await sender.SendAsync(_target, new PushPayload("x", 1), CancellationToken.None);

        var header = _lastRequest!.Headers["Authorization"]!;
        Assert.StartsWith("vapid t=", header);
        Assert.EndsWith($", k={_vapid.PublicKey}", header);

        var jwt = header["vapid t=".Length..header.IndexOf(", k=", StringComparison.Ordinal)];
        VapidSignerTests.AssertValidJwt(jwt, _vapid.PublicKey, expectedAudience: _target.Endpoint[..^"/push/abc123".Length], _options.VapidSubject!, _clock.GetUtcNow());
    }

    [Theory]
    [InlineData(404)]
    [InlineData(410)]
    public async Task A_404_or_410_means_the_subscription_is_gone(int statusCode)
    {
        _statusToReturn = statusCode;
        using var sender = CreateSender();

        Assert.Equal(PushSendStatus.SubscriptionGone, await sender.SendAsync(_target, new PushPayload("x", 1), CancellationToken.None));
    }

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(202)]
    public async Task Any_2xx_is_success(int statusCode)
    {
        _statusToReturn = statusCode;
        using var sender = CreateSender();

        Assert.Equal(PushSendStatus.Sent, await sender.SendAsync(_target, new PushPayload("x", 1), CancellationToken.None));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(413)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task Other_errors_are_transient_and_do_not_leak_subscription_data_to_the_log(int statusCode)
    {
        _statusToReturn = statusCode;
        using var sender = CreateSender();

        var status = await sender.SendAsync(_target, new PushPayload("x", 1), CancellationToken.None);

        Assert.Equal(PushSendStatus.Failed, status);
        Assert.NotEmpty(_logs);
        Assert.All(_logs, line =>
        {
            Assert.DoesNotContain(_target.Endpoint, line);
            Assert.DoesNotContain(_target.P256dh, line);
            Assert.DoesNotContain(_target.Auth, line);
            Assert.DoesNotContain(_vapid.PrivateKey, line);
        });
    }

    [Fact]
    public async Task The_push_service_reason_is_logged_to_help_diagnose_rejections()
    {
        _statusToReturn = 403;
        _bodyToReturn = """{"reason":"BadJwtToken"}""";
        using var sender = CreateSender();

        await sender.SendAsync(_target, new PushPayload("x", 1), CancellationToken.None);

        Assert.Contains(_logs, line => line.Contains("403") && line.Contains("BadJwtToken"));
    }

    [Fact]
    public async Task An_arbitrary_reason_is_not_copied_into_the_log()
    {
        _statusToReturn = 500;
        _bodyToReturn = """{"reason":"line1\nsecret-looking content with spaces"}""";
        using var sender = CreateSender();

        await sender.SendAsync(_target, new PushPayload("x", 1), CancellationToken.None);

        Assert.DoesNotContain(_logs, line => line.Contains("secret-looking"));
    }

    [Fact]
    public async Task A_redirect_is_not_followed()
    {
        _statusToReturn = 302;
        using var sender = CreateSender();

        Assert.Equal(PushSendStatus.Failed, await sender.SendAsync(_target, new PushPayload("x", 1), CancellationToken.None));
    }

    [Fact]
    public async Task Malformed_subscription_keys_are_a_failure_not_a_crash()
    {
        using var sender = CreateSender();

        var status = await sender.SendAsync(_target with { P256dh = "not-a-key" }, new PushPayload("x", 1), CancellationToken.None);

        Assert.Equal(PushSendStatus.Failed, status);
        Assert.Null(_lastRequest);
    }

    [Fact]
    public async Task An_unreachable_push_service_is_a_transient_failure()
    {
        _listener.Stop();
        using var sender = CreateSender();

        Assert.Equal(PushSendStatus.Failed, await sender.SendAsync(_target, new PushPayload("x", 1), CancellationToken.None));
    }

    [Fact]
    public void Sender_is_disabled_without_vapid_keys()
    {
        using var sender = CreateSender(new PushOptions());

        Assert.False(sender.IsEnabled);
        Assert.Null(sender.VapidPublicKey);
    }

    [Fact]
    public async Task A_disabled_sender_never_sends()
    {
        using var sender = CreateSender(new PushOptions());

        Assert.Equal(PushSendStatus.Failed, await sender.SendAsync(_target, new PushPayload("x", 1), CancellationToken.None));
        Assert.Null(_lastRequest);
    }

    public void Dispose()
    {
        _listener.Close();
        _browserKey.Dispose();
    }

    private sealed class ListLogger<T>(List<string> lines) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lines.Add(formatter(state, exception));
            if (exception is not null)
            {
                lines.Add(exception.ToString());
            }
        }
    }
}
