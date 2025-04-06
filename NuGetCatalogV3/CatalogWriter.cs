namespace JsonLog.NuGetCatalogV3;

public class CatalogWriter
{
    private const int MaxItemsPerPage = 2750;
    private readonly ICatalogWriterStore _store;

    public CatalogWriter(ICatalogWriterStore store)
    {
        _store = store;
    }

    public async Task WriteAsync(CatalogCommit commit, string leafBaseUrl)
    {
        if (commit.Events.Count == 0)
        {
            throw new ArgumentException("The commit must have at least one item.", nameof(commit.Events));
        }

        var indexResult = await _store.ReadIndexAsync();

        CatalogIndex? index = indexResult?.Value;

        if (index is null)
        {
            index = new CatalogIndex
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
                Context = CatalogIndexContext.Default,
            };
        }

        CatalogPageItem? latestPageItem = GetLatestPageItem(index);
        if (latestPageItem is null && indexResult is not null)
        {
            throw new InvalidOperationException("The index must have at least one page item.");
        }

        if (latestPageItem is not null && latestPageItem.Count + commit.Events.Count <= MaxItemsPerPage)
        {
            var latestPageResult = await _store.ReadPageAsync(latestPageItem.Id);

            var latestPage = latestPageResult.Value;
            latestPage.Items.AddRange(GenerateLeafItems(commit, leafBaseUrl));
            latestPage.CommitId = commit.Id;
            latestPage.CommitTimestamp = commit.CommitTimestamp;
            latestPage.Count = latestPage.Items.Count;

            await _store.UpdatePageAsync(latestPage, latestPageResult.ETag);

            latestPageItem.CommitId = latestPage.CommitId;
            latestPageItem.CommitTimestamp = latestPage.CommitTimestamp;
            latestPageItem.Count = latestPage.Items.Count;
        }
        else
        {
            var newPage = new CatalogPage
            {
                Id = GeneratePageId(commit.BaseUrl, index.Items.Count),
                Type = "CatalogPage",
                CommitId = commit.Id,
                CommitTimestamp = commit.CommitTimestamp,
                Count = commit.Events.Count,
                Parent = index.Id,
                Items = GenerateLeafItems(commit, leafBaseUrl).ToList(),
                Context = CatalogIndexContext.Default,
            };

            await _store.AddPageAsync(newPage);

            index.Items.Add(new CatalogPageItem
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

    private static CatalogPageItem? GetLatestPageItem(CatalogIndex index)
    {
        CatalogPageItem? latestPageItem = null;
        for (var i = index.Items.Count - 1; i >= 0 && latestPageItem is null; i--)
        {
            if (index.Items[i].CommitId == index.CommitId)
            {
                latestPageItem = index.Items[i];
            }
        }

        return latestPageItem;
    }

    private static List<CatalogLeafItem> GenerateLeafItems(CatalogCommit commit, string leafBaseUrl)
    {
        var leafItems = new List<CatalogLeafItem>(commit.Events.Count);

        foreach (var e in commit.Events)
        {
            leafItems.Add(new CatalogLeafItem
            {
                Id = GenerateLeafId(leafBaseUrl, commit.CommitTimestamp, e.NuGetId, e.NuGetVersion),
                CommitId = commit.Id,
                CommitTimestamp = commit.CommitTimestamp,
                NuGetId = e.NuGetId,
                NuGetVersion = e.NuGetVersion,
                Type = e.Type,
            });
        }

        return leafItems;
    }

    private static string GenerateIndexId(string baseUrl)
    {
        return $"{baseUrl}index.json";
    }

    private static string GeneratePageId(string baseUrl, int pageIndex)
    {
        return $"{baseUrl}page{pageIndex}.json";
    }

    private static string GenerateLeafId(string leafBaseUrl, DateTimeOffset commitTimestamp, string nuGetId, string nuGetVersion)
    {
        return $"{leafBaseUrl}data/{commitTimestamp:yyyy.MM.dd.HH.mm.ss}/{nuGetId.ToLowerInvariant()}.{nuGetVersion.ToLowerInvariant()}.json";
    }
}
