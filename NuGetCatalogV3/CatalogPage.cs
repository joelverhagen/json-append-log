using System.Text.Json.Serialization;

namespace JsonLog.NuGetCatalogV3;

public class CatalogPage
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

    [JsonPropertyName("parent")]
    public required string Parent { get; set; }

    [JsonPropertyName("items")]
    public required List<CatalogLeafItem> Items { get; set; }

    [JsonPropertyName("@context")]
    public required CatalogIndexContext Context { get; set; }
}
