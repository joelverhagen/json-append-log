namespace JsonLog.NuGetCatalogV3;

public record ReadResult<T>(T Value, string ETag);

public interface ICatalogWriterStore
{
    Task<ReadResult<CatalogIndex>?> ReadIndexAsync();
    Task AddIndexAsync(CatalogIndex index);
    Task UpdateIndexAsync(CatalogIndex index, string etag);
    Task<ReadResult<CatalogPage>> ReadPageAsync(string id);
    Task AddPageAsync(CatalogPage page);
    Task UpdatePageAsync(CatalogPage page, string etag);
}

public enum WriteResultType
{
    Conflict,
    Success,
}
