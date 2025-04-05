using System.Text.Json.Serialization;

namespace JsonLog.NuGetCatalogV3;

public class Index
{
    [JsonPropertyName("@id")]
    public required string Id { get; set; }

    [JsonPropertyName("@type")]
    public required List<string> Type { get; set; }

    [JsonPropertyName("commitId")]
    public required string CommitId { get; set; }

    [JsonPropertyName("commitTimeStamp")]
    [JsonConverter(typeof(DateTimeOffsetJsonConverter))]
    public required DateTimeOffset CommitTimestamp { get; set; }

    [JsonPropertyName("count")]
    public required int Count { get; set; }

    [JsonPropertyName("nuget:lastCreated")]
    [JsonConverter(typeof(DateTimeOffsetJsonConverter))]
    public required DateTimeOffset NuGetLastCreated { get; set; }

    [JsonPropertyName("nuget:lastDeleted")]
    [JsonConverter(typeof(DateTimeOffsetJsonConverter))]
    public required DateTimeOffset NuGetLastDeleted { get; set; }

    [JsonPropertyName("nuget:lastEdited")]
    [JsonConverter(typeof(DateTimeOffsetJsonConverter))]
    public required DateTimeOffset NuGetLastEdited { get; set; }

    [JsonPropertyName("items")]
    public required List<PageItem> Items { get; set; }

    [JsonPropertyName("@context")]
    public required IndexContext Context { get; set; }
}
