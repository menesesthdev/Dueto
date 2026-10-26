using Dueto.Core.Domain;

namespace Dueto.Core.Pairing;

/// <summary>Conversão entre <see cref="DeviceKind"/> e o texto do contrato ("desktop" | "mobile").</summary>
public static class DeviceKinds
{
    public static bool TryParse(string? value, out DeviceKind kind)
    {
        if (string.Equals(value, "desktop", StringComparison.OrdinalIgnoreCase))
        {
            kind = DeviceKind.Desktop;
            return true;
        }

        if (string.Equals(value, "mobile", StringComparison.OrdinalIgnoreCase))
        {
            kind = DeviceKind.Mobile;
            return true;
        }

        kind = default;
        return false;
    }

    public static string ToWire(DeviceKind kind) => kind == DeviceKind.Desktop ? "desktop" : "mobile";
}
