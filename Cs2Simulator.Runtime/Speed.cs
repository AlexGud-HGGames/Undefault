using System;
using System.Globalization;

namespace Cs2Simulator.Runtime;

public readonly record struct Speed(double Multiplier)
{
    public static Speed Normal => new(1d);
    public static Speed Max { get; } = new(double.PositiveInfinity);

    public bool IsMax => double.IsInfinity(Multiplier);

    public TimeSpan Scale(TimeSpan nominal)
    {
        if (IsMax || Multiplier <= 0)
        {
            return TimeSpan.Zero;
        }

        if (nominal <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var scaledMs = nominal.TotalMilliseconds / Multiplier;
        return TimeSpan.FromMilliseconds(scaledMs);
    }

    public override string ToString()
    {
        return IsMax ? "max" : Multiplier.ToString("0.##", CultureInfo.InvariantCulture) + "x";
    }

    public static Speed Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Normal;
        }

        var trimmed = text.Trim();
        if (trimmed.EndsWith("x", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^1];
        }

        if (string.Equals(trimmed, "max", StringComparison.OrdinalIgnoreCase))
        {
            return Max;
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && value > 0)
        {
            return new Speed(value);
        }

        throw new FormatException($"Unrecognized speed value '{text}'. Use 1, 2, 5, or max.");
    }
}
