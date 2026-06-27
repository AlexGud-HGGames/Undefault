namespace GsiHost.Services;

/// <summary>
/// Parses and represents a Windows global hotkey definition (modifier flag bits
/// plus a virtual-key code) consumed by <see cref="WindowsHotkeyService"/>. The
/// parsing logic is pure and platform-agnostic so it can be unit-tested without
/// driving the Win32 <c>RegisterHotKey</c> message loop.
/// </summary>
public readonly record struct HotkeyDefinition(uint Modifiers, uint VirtualKey)
{
    /// <summary>Win32 <c>MOD_ALT</c> modifier bit.</summary>
    public const uint ModAlt = 0x0001;

    /// <summary>Win32 <c>MOD_CONTROL</c> modifier bit.</summary>
    public const uint ModControl = 0x0002;

    /// <summary>Win32 <c>MOD_SHIFT</c> modifier bit.</summary>
    public const uint ModShift = 0x0004;

    /// <summary>Win32 <c>MOD_WIN</c> modifier bit.</summary>
    public const uint ModWin = 0x0008;

    /// <summary>
    /// Parses a hotkey string such as <c>"Ctrl+Alt+P"</c> or <c>"Shift+F9"</c>
    /// into combined modifier flag bits plus a virtual-key code. Modifier names
    /// (<c>Ctrl</c>/<c>Control</c>, <c>Alt</c>, <c>Shift</c>, <c>Win</c>/
    /// <c>Windows</c>) and key tokens are matched case-insensitively and
    /// separated by <c>+</c>. Returns <c>null</c> for empty/whitespace input or
    /// when no recognized virtual key is present.
    /// </summary>
    public static HotkeyDefinition? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var modifiers = 0u;
        uint? key = null;
        foreach (var rawPart in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var part = rawPart.Trim();
            if (part.Equals("ctrl", StringComparison.OrdinalIgnoreCase)
                || part.Equals("control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModControl;
                continue;
            }

            if (part.Equals("alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModAlt;
                continue;
            }

            if (part.Equals("shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModShift;
                continue;
            }

            if (part.Equals("win", StringComparison.OrdinalIgnoreCase)
                || part.Equals("windows", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModWin;
                continue;
            }

            key = ParseVirtualKey(part);
        }

        return key.HasValue
            ? new HotkeyDefinition(modifiers, key.Value)
            : null;
    }

    private static uint? ParseVirtualKey(string value)
    {
        if (value.Length == 1)
        {
            var c = char.ToUpperInvariant(value[0]);
            if (c is >= 'A' and <= 'Z')
            {
                return c;
            }

            if (c is >= '0' and <= '9')
            {
                return c;
            }
        }

        if (value.StartsWith('F') && int.TryParse(value[1..], out var functionKey) && functionKey is >= 1 and <= 24)
        {
            return (uint)(0x70 + functionKey - 1);
        }

        return value.ToLowerInvariant() switch
        {
            "space" => 0x20,
            "escape" or "esc" => 0x1B,
            "pause" => 0x13,
            "insert" or "ins" => 0x2D,
            "delete" or "del" => 0x2E,
            "home" => 0x24,
            "end" => 0x23,
            "pageup" => 0x21,
            "pagedown" => 0x22,
            "up" => 0x26,
            "down" => 0x28,
            "left" => 0x25,
            "right" => 0x27,
            _ => null
        };
    }
}
