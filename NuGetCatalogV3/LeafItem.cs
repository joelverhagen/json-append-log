using System.Text.Json.Serialization;

namespace JsonLog.NuGetCatalogV3;

public class LeafItem
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

    [JsonPropertyName("nuget:id")]
    public required string NuGetId { get; set; }

    [JsonPropertyName("nuget:version")]
    public required string NuGetVersion { get; set; }
}
