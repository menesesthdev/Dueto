namespace Dueto.Core.Domain;

/// <summary>Um par: um desktop e um celular que compartilham o mesmo estado.</summary>
public class Pair
{
    public Guid Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Device> Devices { get; set; } = [];
}
