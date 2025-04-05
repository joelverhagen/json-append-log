namespace JsonLog.NuGetCatalogV3;

public record ReadResult<T>(T Value, string ETag);

public interface IWriterStore
{
    Task<ReadResult<Index>?> ReadIndexAsync();
    Task<WriteResultType> AddIndexAsync(Index index);
    Task<WriteResultType> UpdateIndexAsync(Index index, string etag);
    Task<ReadResult<Page>> ReadPageAsync(string id);
    Task<WriteResultType> AddPageAsync(Page page);
    Task<WriteResultType> UpdatePageAsync(Page page, string etag);
}

public enum WriteResultType
{
    Conflict,
    Success,
}