using System.Text.Json.Serialization;

namespace JsonLog.NuGetCatalogV3;

public class ContextType
{
    [JsonPropertyName("@type")]
    public required string Type { get; set; }
}
