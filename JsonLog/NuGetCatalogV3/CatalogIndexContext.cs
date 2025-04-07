using System.Text.Json.Serialization;

namespace JsonLog.NuGetCatalogV3;

public class CatalogIndexContext
{
    public static CatalogIndexContext Default => new CatalogIndexContext
    {
        Vocab = "http://schema.nuget.org/catalog#",
        NuGet = "http://schema.nuget.org/schema#",
        Items = new CatalogContextListType { Id = "item", Container = "@set" },
        Parent = new CatalogContextType { Type = "@id" },
        CommitTimestamp = new CatalogContextType { Type = "http://www.w3.org/2001/XMLSchema#dateTime" },
        NuGetLastCreated = new CatalogContextType { Type = "http://www.w3.org/2001/XMLSchema#dateTime" },
        NuGetLastEdited = new CatalogContextType { Type = "http://www.w3.org/2001/XMLSchema#dateTime" },
        NuGetLastDeleted = new CatalogContextType { Type = "http://www.w3.org/2001/XMLSchema#dateTime" }
    };

    [JsonPropertyName("@vocab")]
    public required string Vocab { get; set; }

    [JsonPropertyName("nuget")]
    public required string NuGet { get; set; }

    [JsonPropertyName("items")]
    public required CatalogContextListType Items { get; set; }

    [JsonPropertyName("parent")]
    public required CatalogContextType Parent { get; set; }

    [JsonPropertyName("commitTimeStamp")]
    public required CatalogContextType CommitTimestamp { get; set; }

    [JsonPropertyName("nuget:lastCreated")]
    public required CatalogContextType NuGetLastCreated { get; set; }

    [JsonPropertyName("nuget:lastEdited")]
    public required CatalogContextType NuGetLastEdited { get; set; }

    [JsonPropertyName("nuget:lastDeleted")]
    public CatalogContextType? NuGetLastDeleted { get; set; }
}
