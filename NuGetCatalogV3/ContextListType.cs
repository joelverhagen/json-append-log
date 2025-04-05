﻿using System.Text.Json.Serialization;

namespace JsonLog.NuGetCatalogV3;

public class ContextListType
{
    [JsonPropertyName("@id")]
    public required string Id { get; set; }

    [JsonPropertyName("@container")]
    public required string Container { get; set; }
}
