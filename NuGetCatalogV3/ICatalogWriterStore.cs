namespace JsonLog.NuGetCatalogV3;

public record ReadResult<T>(T Value, string ETag);

public interface ICatalogWriterStore
{
    Task<ReadResult<CatalogIndex>?> ReadIndexAsync();
    Task<WriteResultType> AddIndexAsync(CatalogIndex index);
    Task<WriteResultType> UpdateIndexAsync(CatalogIndex index, string etag);
    Task<ReadResult<CatalogPage>> ReadPageAsync(string id);
    Task<WriteResultType> AddPageAsync(CatalogPage page);
    Task<WriteResultType> UpdatePageAsync(CatalogPage page, string etag);
}

public enum WriteResultType
{
    Conflict,
    Success,
}