using System.Text.Json.Serialization;

namespace JsonLog.NuGetCatalogV3;

public class IndexContext
{
    public static IndexContext Default => new IndexContext
    {
        Vocab = "http://schema.nuget.org/catalog#",
        NuGet = "http://schema.nuget.org/schema#",
        Items = new ContextListType { Id = "item", Container = "@set" },
        Parent = new ContextType { Type = "@id" },
        CommitTimestamp = new ContextType { Type = "http://www.w3.org/2001/XMLSchema#dateTime" },
        NuGetLastCreated = new ContextType { Type = "http://www.w3.org/2001/XMLSchema#dateTime" },
        NuGetLastEdited = new ContextType { Type = "http://www.w3.org/2001/XMLSchema#dateTime" },
        NuGetLastDeleted = new ContextType { Type = "http://www.w3.org/2001/XMLSchema#dateTime" }
    };

    [JsonPropertyName("@vocab")]
    public required string Vocab { get; set; }

    [JsonPropertyName("nuget")]
    public required string NuGet { get; set; }

    [JsonPropertyName("items")]
    public required ContextListType Items { get; set; }

    [JsonPropertyName("parent")]
    public required ContextType Parent { get; set; }

    [JsonPropertyName("commitTimeStamp")]
    public required ContextType CommitTimestamp { get; set; }

    [JsonPropertyName("nuget:lastCreated")]
    public required ContextType NuGetLastCreated { get; set; }

    [JsonPropertyName("nuget:lastEdited")]
    public required ContextType NuGetLastEdited { get; set; }

    [JsonPropertyName("nuget:lastDeleted")]
    public required ContextType NuGetLastDeleted { get; set; }
}
