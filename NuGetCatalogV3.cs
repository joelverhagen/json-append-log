using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DiffEngine;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace JsonLog.NuGetCatalogV3;

public class Client
{
    private static JsonSerializerOptions LegacyEncoder => new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly HttpClient _httpClient;
    private readonly bool _validateRoundTrip;

    public Client(HttpClient httpClient, bool validateRoundTrip)
    {
        _httpClient = httpClient;
        _validateRoundTrip = validateRoundTrip;
    }

    public async Task<Index> ReadIndexAsync(string url)
    {
        return await ReadAsync<Index>(url, LegacyEncoder);
    }

    public async Task<Page> ReadPageAsync(string url)
    {
        return await ReadAsync<Page>(url, LegacyEncoder);
    }

    private async Task<T> ReadAsync<T>(string url, JsonSerializerOptions options)
    {
        if (_validateRoundTrip)
        {
            var originalJson = await _httpClient.GetStringAsync(url);
            var deserialized = JsonSerializer.Deserialize<T>(originalJson);
            if (deserialized is null)
            {
                throw new JsonException("Deserialized model should not be null.");
            }

            JsonUtility.VerifyRoundTrip(originalJson, deserialized, options);

            return deserialized;
        }
        else
        {
            using var stream = await _httpClient.GetStreamAsync(url);
            var deserialized = await JsonSerializer.DeserializeAsync<T>(stream);
            if (deserialized is null)
            {
                throw new JsonException("Deserialized model should not be null.");
            }

            return deserialized;
        }
    }
}

public interface IWriterStore
{
    Task<Index> ReadIndexAsync(string id);
    Task WriteIndexAsync(Index index);
    Task<Page> ReadPageAsync(string id);
    Task WritePageAsync(Page page);
}

public class InMemoryWriterStore : IWriterStore
{
    private readonly Dictionary<string, Index> _indexStore = new();
    private readonly Dictionary<string, Page> _pageStore = new();

    public Task<Index> ReadIndexAsync(string id)
    {
        if (_indexStore.TryGetValue(id, out var index))
        {
            return Task.FromResult(index);
        }

        throw new KeyNotFoundException($"Index with ID {id} not found.");
    }

    public Task WriteIndexAsync(Index index)
    {
        _indexStore[index.Id] = index;
        return Task.CompletedTask;
    }

    public Task<Page> ReadPageAsync(string id)
    {
        if (_pageStore.TryGetValue(id, out var page))
        {
            return Task.FromResult(page);
        }

        throw new KeyNotFoundException($"Page with ID {id} not found.");
    }

    public Task WritePageAsync(Page page)
    {
        _pageStore[page.Id] = page;
        return Task.CompletedTask;
    }
}

public class Writer
{
    private const int MaxItemsPerPage = 2750;
    private readonly IWriterStore _store;

    public Writer(IWriterStore store)
    {
        _store = store;
    }

    public async Task WriteAsync(Index index, List<LeafItem> newItems, string baseUrl)
    {
        if (newItems == null || newItems.Count == 0)
        {
            throw new ArgumentException("No items to write.", nameof(newItems));
        }

        var latestPageItem = index.Items.Last();
        var latestPage = await _store.ReadPageAsync(latestPageItem.Id);

        if (latestPage.Items.Count + newItems.Count <= MaxItemsPerPage)
        {
            latestPage.Items.AddRange(newItems);
            latestPage.Count = latestPage.Items.Count;
            await _store.WritePageAsync(latestPage);
        }
        else
        {
            var remainingItems = new List<LeafItem>(newItems);

            // Fill the current page to its maximum capacity
            var itemsToAdd = MaxItemsPerPage - latestPage.Items.Count;
            latestPage.Items.AddRange(remainingItems.Take(itemsToAdd));
            latestPage.Count = latestPage.Items.Count;
            await _store.WritePageAsync(latestPage);

            remainingItems = remainingItems.Skip(itemsToAdd).ToList();

            // Create new pages for the remaining items
            while (remainingItems.Any())
            {
                var newPageItems = remainingItems.Take(MaxItemsPerPage).ToList();
                remainingItems = remainingItems.Skip(MaxItemsPerPage).ToList();

                var newPage = new Page
                {
                    Id = GeneratePageUrl(baseUrl, index.Items.Count + 1),
                    Type = "catalog:Page",
                    CommitId = Guid.NewGuid().ToString(),
                    CommitTimestamp = DateTimeOffset.UtcNow,
                    Count = newPageItems.Count,
                    Parent = index.Id,
                    Items = newPageItems,
                    Context = IndexContext.Default
                };

                await _store.WritePageAsync(newPage);

                var newPageItem = new PageItem
                {
                    Id = newPage.Id,
                    Type = newPage.Type,
                    CommitId = newPage.CommitId,
                    CommitTimeStamp = newPage.CommitTimestamp,
                    Count = newPage.Count
                };

                index.Items.Add(newPageItem);
            }
        }

        // Update the index
        index.CommitId = Guid.NewGuid().ToString();
        index.CommitTimestamp = DateTimeOffset.UtcNow;
        index.Count = index.Items.Sum(item => item.Count);
        await _store.WriteIndexAsync(index);
    }

    private string GeneratePageUrl(string baseUrl, int pageIndex)
    {
        return $"{baseUrl}/page-{pageIndex}.json";
    }
}

public static class JsonUtility
{
    public static void VerifyRoundTrip<T>(string originalJson, T deserialized, JsonSerializerOptions options)
    {
        var serializedJson = JsonSerializer.Serialize(deserialized, options);
        if (originalJson != serializedJson)
        {
            var originalJsonRoundTrip = JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonDocument>(originalJson, options), options);
            if (originalJsonRoundTrip != originalJson)
            {
                throw new JsonException("The original JSON should be round-trippable.");
            }

            var indentedOptions = new JsonSerializerOptions(options);
            indentedOptions.WriteIndented = true;

            var originalJsonIndented = JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonDocument>(originalJson, indentedOptions), indentedOptions);
            var serializedJsonIndented = JsonSerializer.Serialize(deserialized, indentedOptions);

            var diff = InlineDiffBuilder.Diff(originalJsonIndented, serializedJsonIndented);
            var changedCount = diff.Lines.Count(line => line.Type != ChangeType.Unchanged);

            var originalTemp = Path.GetTempFileName();
            var serializedTemp = Path.GetTempFileName();
            try
            {
                if (changedCount == 0)
                {
                    File.WriteAllText(originalTemp, originalJson);
                    File.WriteAllText(serializedTemp, serializedJson);
                }
                else
                {
                    File.WriteAllText(originalTemp, originalJsonIndented);
                    File.WriteAllText(serializedTemp, serializedJsonIndented);
                }

                var launchResult = DiffRunner.Launch(originalTemp, serializedTemp);
                if (launchResult == LaunchResult.NoDiffToolFound)
                {
                    throw new JsonException("No diff tool found.");
                }
            }
            finally
            {
                File.Delete(originalTemp);
                File.Delete(serializedTemp);
            }

            throw new JsonException("The deserialized modle should serialize to the same string.");
        }
    }
}

public class DateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateTimeOffset.Parse(reader.GetString()!, CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new JsonException("Only UTC timestamps are supported.");
        }

        writer.WriteStringValue(value.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'FFFFFFF'Z'", CultureInfo.InvariantCulture));
    }
}

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

public class PageItem
{
    [JsonPropertyName("@id")]
    public required string Id { get; set; }

    [JsonPropertyName("@type")]
    public required string Type { get; set; }

    [JsonPropertyName("commitId")]
    public required string CommitId { get; set; }

    [JsonPropertyName("commitTimeStamp")]
    [JsonConverter(typeof(DateTimeOffsetJsonConverter))]
    public required DateTimeOffset CommitTimeStamp { get; set; }

    [JsonPropertyName("count")]
    public required int Count { get; set; }
}

public class Page
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
    public required List<LeafItem> Items { get; set; }

    [JsonPropertyName("@context")]
    public required IndexContext Context { get; set; }
}

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

public class ContextListType
{
    [JsonPropertyName("@id")]
    public required string Id { get; set; }

    [JsonPropertyName("@container")]
    public required string Container { get; set; }
}

public class ContextType
{
    [JsonPropertyName("@type")]
    public required string Type { get; set; }
}
