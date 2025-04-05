namespace JsonLog.NuGetCatalogV3;

public class Writer
{
    private const int MaxItemsPerPage = 2750;
    private readonly IWriterStore _store;

    public Writer(IWriterStore store)
    {
        _store = store;
    }

    public async Task WriteAsync(Commit commit)
    {
        if (commit.Events.Count == 0)
        {
            throw new ArgumentException("The commit must have at least one item.", nameof(commit.Events));
        }

        var indexResult = await _store.ReadIndexAsync();

        Index? index = indexResult?.Value;

        if (index is null)
        {
            index = new Index
            {
                Id = GenerateIndexId(commit.BaseUrl),
                Type = ["CatalogRoot", "AppendOnlyCatalog", "Permalink"],
                CommitId = commit.Id,
                CommitTimestamp = commit.CommitTimestamp,
                Count = 1,
                NuGetLastCreated = commit.NuGetLastCreated,
                NuGetLastDeleted = commit.NuGetLastDeleted,
                NuGetLastEdited = commit.NuGetLastEdited,
                Items = [],
                Context = IndexContext.Default,
            };
        }

        PageItem? latestPageItem = index.Items.MaxBy(item => item.CommitTimestamp);

        if (latestPageItem is not null && latestPageItem.Count + commit.Events.Count <= MaxItemsPerPage)
        {
            var latestPageResult = await _store.ReadPageAsync(latestPageItem.Id);

            var latestPage = latestPageResult.Value;
            latestPage.Items.AddRange(GenerateLeafItems(commit));
            latestPage.CommitId = commit.Id;
            latestPage.CommitTimestamp = commit.CommitTimestamp;
            latestPage.Count = latestPage.Items.Count;

            await _store.UpdatePageAsync(latestPage, latestPageResult.ETag);

            latestPageItem.Count = latestPage.Items.Count;
        }
        else
        {
            var newPage = new Page
            {
                Id = GeneratePageId(commit.BaseUrl, index.Items.Count),
                Type = "CatalogPage",
                CommitId = commit.Id,
                CommitTimestamp = commit.CommitTimestamp,
                Count = commit.Events.Count,
                Parent = index.Id,
                Items = GenerateLeafItems(commit).ToList(),
                Context = IndexContext.Default,
            };

            await _store.AddPageAsync(newPage);

            index.Items.Add(new PageItem
            {
                Id = newPage.Id,
                Type = newPage.Type,
                CommitId = newPage.CommitId,
                CommitTimestamp = newPage.CommitTimestamp,
                Count = newPage.Count,
            });
            index.Count = index.Items.Count;
        }
        
        index.CommitId = commit.Id;
        index.CommitTimestamp = commit.CommitTimestamp;

        if (indexResult is null)
        {
            await _store.AddIndexAsync(index);
        }
        else
        {
            await _store.UpdateIndexAsync(index, indexResult.ETag);
        }
    }

    private static IEnumerable<LeafItem> GenerateLeafItems(Commit commit)
    {
        return commit.Events.Select(e => new LeafItem
        {
            Id = GenerateLeafId(commit.BaseUrl, commit.CommitTimestamp, e.NuGetId, e.NuGetVersion),
            CommitId = commit.Id,
            CommitTimestamp = commit.CommitTimestamp,
            NuGetId = e.NuGetId,
            NuGetVersion = e.NuGetVersion,
            Type = e.Type,
        });
    }

    private static string GenerateIndexId(string baseUrl)
    {
        return $"{baseUrl}index.json";
    }

    private static string GeneratePageId(string baseUrl, int pageIndex)
    {
        return $"{baseUrl}page{pageIndex}.json";
    }

    private static string GenerateLeafId(string baseUrl, DateTimeOffset commitTimestamp, string nuGetId, string nuGetVersion)
    {
        return $"{baseUrl}data/{commitTimestamp:yyyy.MM.dd.HH.mm.ss}/{nuGetId.ToLowerInvariant()}/{nuGetVersion.ToLowerInvariant()}.json";
    }
}
