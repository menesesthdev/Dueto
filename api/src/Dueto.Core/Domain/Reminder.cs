namespace Dueto.Core.Domain;

/// <summary>
/// Reenvio programado da notificação de push enquanto o celular não confirma.
/// O estado vive no banco para sobreviver a reinício do container.
/// </summary>
public class Reminder
{
    public Guid Id { get; set; }

    public Guid DeviceId { get; set; }

    /// <summary>Versão do estado que este lembrete cobre.</summary>
    public long Version { get; set; }

    /// <summary>Ordem do reenvio: 1, 2 ou 3.</summary>
    public int Sequence { get; set; }

    public DateTimeOffset DueAt { get; set; }

    public ReminderStatus Status { get; set; }
}
