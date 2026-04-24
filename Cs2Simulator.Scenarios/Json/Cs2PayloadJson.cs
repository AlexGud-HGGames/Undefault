using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Cs2Simulator.Scenarios.Models;

namespace Cs2Simulator.Scenarios.Json;

public static class Cs2PayloadJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var o = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
        o.MakeReadOnly();
        return o;
    }

    public static string Serialize(Cs2Payload payload)
    {
        return JsonSerializer.Serialize(payload, Options);
    }
}
