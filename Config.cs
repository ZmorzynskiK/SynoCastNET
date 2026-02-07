using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace SynoCastNET;

public record Source(
    string type,
    string url,
    string name,
    int maxItems,
    string language,
    string? container = null,
    int? maxDownloads = null,
    int? minDurationSecs = 60       // do we want to skip short videos? default to 1 minute, as many shorts are just noise and not worth the effort
);

public record Config(
    Source[] sources,
    string? outputDirectory = null
);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Source))]
internal partial class SourceSerializationContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Config))]
internal partial class ConfigSerializationContext : JsonSerializerContext
{
}