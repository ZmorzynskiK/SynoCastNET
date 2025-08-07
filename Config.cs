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
    int? maxDownloads = null
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