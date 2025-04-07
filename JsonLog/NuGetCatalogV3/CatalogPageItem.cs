using System.Text.Json.Serialization;

namespace JsonLog.NuGetCatalogV3;

public class CatalogPageItem
{
    [JsonPropertyName("@id")]
    public required string Id { get; set; }

    [JsonPropertyName("@type")]
    public required string Type { get; set; }

    [JsonPropertyName("commitId")]
    public required string CommitId { get; set; }

    [JsonPropertyName("commitTimeStamp")]
    [JsonConverter(typeof(DateTimeOffsetJsonConverter))]
    public required DateTimeOffset CommitTimestamp { get; set; }

    [JsonPropertyName("count")]
    public required int Count { get; set; }
}
