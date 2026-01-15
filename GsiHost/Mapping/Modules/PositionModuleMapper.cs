using System.Globalization;
using System.Text.Json;
using Core.Models;
using GsiHost.Dtos;

namespace GsiHost.Mapping.Modules;

public sealed class PositionModuleMapper : ISnapshotModuleMapper
{
    public ISnapshotModule? Map(GsiPayloadDto payload)
    {
        var positionElement = payload.Player?.Position;
        if (!positionElement.HasValue)
        {
            return null;
        }

        var position = ParsePosition(positionElement.Value);
        return new PositionModule(position, IsMoving: false);
    }

    private static Vector3 ParsePosition(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
        {
            return Vector3.Zero;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => ParseVectorFromString(element.GetString()),
            JsonValueKind.Array => ParseVectorFromArray(element),
            JsonValueKind.Object => ParseVectorFromObject(element),
            _ => Vector3.Zero
        };
    }

    private static Vector3 ParseVectorFromString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Vector3.Zero;
        }

        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3)
        {
            return Vector3.Zero;
        }

        return new Vector3(
            ParseFloat(parts[0]),
            ParseFloat(parts[1]),
            ParseFloat(parts[2])
        );
    }

    private static Vector3 ParseVectorFromArray(JsonElement value)
    {
        if (value.GetArrayLength() < 3)
        {
            return Vector3.Zero;
        }

        return new Vector3(
            ParseFloat(value[0]),
            ParseFloat(value[1]),
            ParseFloat(value[2])
        );
    }

    private static Vector3 ParseVectorFromObject(JsonElement value)
    {
        if (!value.TryGetProperty("x", out var x)
            || !value.TryGetProperty("y", out var y)
            || !value.TryGetProperty("z", out var z))
        {
            return Vector3.Zero;
        }

        return new Vector3(
            ParseFloat(x),
            ParseFloat(y),
            ParseFloat(z)
        );
    }

    private static float ParseFloat(string? raw)
    {
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return 0f;
    }

    private static float ParseFloat(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return ParseFloat(value.GetString());
        }

        return 0f;
    }
}
