namespace Dueto.Core.Domain;

/// <summary>Tipo de wallpaper. Animados existem só no desktop.</summary>
public enum WallpaperKind
{
    Static,
    Animated,
}

/// <summary>Tipo de dispositivo em um par.</summary>
public enum DeviceKind
{
    Desktop,
    Mobile,
}

/// <summary>Estado de um lembrete de push.</summary>
public enum ReminderStatus
{
    Pending,
    Sent,
    Cancelled,
}
