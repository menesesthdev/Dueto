using System.Text.Json;
using Dueto.Core.Push;
using Dueto.Tests.Infrastructure;

namespace Dueto.Tests.Push;

public class PushOptionsTests
{
    private static readonly PushOptions Defaults = new();
    private static readonly VapidTestKeys Keys = VapidTestKeys.Generate();

    [Theory]
    [InlineData("https://web.push.apple.com/abc123")]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc")]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/abc")]
    [InlineData("https://wns2-par02p.notify.windows.com/w/?token=abc")]
    public void Known_push_services_are_accepted(string endpoint) =>
        Assert.True(Defaults.IsAllowedEndpoint(endpoint));

    [Theory]
    [InlineData("http://web.push.apple.com/abc")]
    [InlineData("https://example.com/push")]
    [InlineData("https://localhost/push")]
    [InlineData("https://127.0.0.1/push")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("https://notpush.apple.com/abc")]
    [InlineData("https://push.apple.com.evil.example/abc")]
    [InlineData("https://user:pass@web.push.apple.com/abc")]
    [InlineData("ftp://web.push.apple.com/abc")]
    [InlineData("not a url")]
    [InlineData("")]
    [InlineData(null)]
    public void Anything_else_is_rejected_to_prevent_ssrf(string? endpoint) =>
        Assert.False(Defaults.IsAllowedEndpoint(endpoint));

    [Fact]
    public void Overlong_endpoints_are_rejected() =>
        Assert.False(Defaults.IsAllowedEndpoint("https://web.push.apple.com/" + new string('a', 2_100)));

    [Fact]
    public void Reminder_delays_default_to_2_15_and_60_minutes() =>
        Assert.Equal([2, 15, 60], Defaults.EffectiveReminderDelaysMinutes);

    [Fact]
    public void Configured_reminder_delays_replace_the_defaults() =>
        Assert.Equal([5], new PushOptions { ReminderDelaysMinutes = [5] }.EffectiveReminderDelaysMinutes);

    [Fact]
    public void Push_is_off_without_vapid_keys()
    {
        Assert.False(Defaults.IsConfigured);
        Assert.Null(Defaults.Validate());
    }

    [Fact]
    public void Vapid_keys_must_be_set_together() =>
        Assert.NotNull(new PushOptions { VapidPublicKey = Keys.PublicKey, VapidSubject = "mailto:a@b.c" }.Validate());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://example.com")]
    [InlineData("admin@example.com")]
    public void Vapid_subject_must_be_mailto_or_https(string? subject) =>
        Assert.NotNull(new PushOptions { VapidPublicKey = Keys.PublicKey, VapidPrivateKey = Keys.PrivateKey, VapidSubject = subject }.Validate());

    [Theory]
    [InlineData("mailto:owner@example.com")]
    [InlineData("https://dueto.example")]
    public void Valid_vapid_configuration_passes(string subject) =>
        Assert.Null(new PushOptions { VapidPublicKey = Keys.PublicKey, VapidPrivateKey = Keys.PrivateKey, VapidSubject = subject }.Validate());

    [Theory]
    [InlineData("not base64 !!", "also bad")]
    [InlineData("AAAA", "AAAA")]
    public void Vapid_keys_must_be_valid_p256_keys(string publicKey, string privateKey) =>
        Assert.NotNull(new PushOptions { VapidPublicKey = publicKey, VapidPrivateKey = privateKey, VapidSubject = "mailto:a@b.c" }.Validate());

    [Fact]
    public void Swapped_vapid_keys_are_rejected() =>
        Assert.NotNull(new PushOptions { VapidPublicKey = Keys.PrivateKey, VapidPrivateKey = Keys.PublicKey, VapidSubject = "mailto:a@b.c" }.Validate());

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1_441)]
    public void Reminder_delays_must_be_in_range(int delay) =>
        Assert.NotNull(new PushOptions { ReminderDelaysMinutes = [delay] }.Validate());

    [Fact]
    public void Payload_contains_only_wallpaper_id_and_version()
    {
        var json = JsonSerializer.Serialize(new PushPayload("neon-tide", 7), JsonSerializerOptions.Web);

        Assert.Equal("""{"wallpaperId":"neon-tide","version":7}""", json);
    }
}
