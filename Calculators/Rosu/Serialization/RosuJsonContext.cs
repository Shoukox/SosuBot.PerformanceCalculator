using System.Text.Json.Serialization;

namespace SosuBot.PerformanceCalculator;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(RosuNativeRequest))]
[JsonSerializable(typeof(RosuNativeResponse))]
internal partial class RosuJsonContext : JsonSerializerContext
{
}
