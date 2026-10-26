namespace Dueto.Tests.Infrastructure;

/// <summary>Relógio controlável para testar expiração sem esperar de verdade.</summary>
public sealed class TestClock : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan amount) => _now += amount;
}
