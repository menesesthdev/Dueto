namespace Dueto.Core.Push;

public interface IReminderProcessor
{
    /// <summary>Dispara os lembretes vencidos e ainda pendentes. Devolve quantos foram reivindicados.</summary>
    Task<int> ProcessDueAsync(CancellationToken cancellationToken);
}
