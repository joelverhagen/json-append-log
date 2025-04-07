using JsonLog.Utility;

namespace JsonLog.NuGetCatalogV3;

public class MemoryCatalogWriterStore : ICatalogWriterStore
{
    private readonly SemaphoreSlim _lock = new(1);
    private ReadResult<CatalogIndex>? _index;
    private long _indexSize;
    private readonly Dictionary<string, (ReadResult<CatalogPage> Result, long Size)> _pages = new();
    private readonly TokenProvider _tokenProvider;

    public MemoryCatalogWriterStore(TokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    public async Task<ReadResult<CatalogIndex>?> ReadIndexAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_index is null)
            {
                return null;
            }

            return _index;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<WriteResultType> AddIndexAsync(CatalogIndex index)
    {
        await _lock.WaitAsync();
        try
        {
            if (_index is not null)
            {
                return WriteResultType.Conflict;
            }

            _index = new ReadResult<CatalogIndex>(index, _tokenProvider.GetETag());
            _indexSize = JsonUtility.GetJsonSize(index, CatalogClient.LegacyEncoder);
            return WriteResultType.Success;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<WriteResultType> UpdateIndexAsync(CatalogIndex index, string etag)
    {
        await _lock.WaitAsync();
        try
        {
            if (_index is null)
            {
                return WriteResultType.Conflict;
            }

            if (_index.ETag != etag)
            {
                return WriteResultType.Conflict;
            }

            _index = new ReadResult<CatalogIndex>(index, _tokenProvider.GetETag());
            _indexSize = JsonUtility.GetJsonSize(index, CatalogClient.LegacyEncoder);
            return WriteResultType.Success;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ReadResult<CatalogPage>> ReadPageAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            if (!_pages.TryGetValue(id, out var info))
            {
                throw new InvalidOperationException($"Page {id} not found.");
            }

            return info.Result;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<WriteResultType> AddPageAsync(CatalogPage page)
    {
        await _lock.WaitAsync();
        try
        {
            if (_pages.ContainsKey(page.Id))
            {
                return WriteResultType.Conflict;
            }

            var pageSize = JsonUtility.GetJsonSize(page, CatalogClient.LegacyEncoder);
            _pages[page.Id] = (new ReadResult<CatalogPage>(page, _tokenProvider.GetETag()), pageSize);
            return WriteResultType.Success;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<WriteResultType> UpdatePageAsync(CatalogPage page, string etag)
    {
        await _lock.WaitAsync();
        try
        {
            if (!_pages.TryGetValue(page.Id, out var info))
            {
                return WriteResultType.Conflict;
            }

            if (info.Result.ETag != etag)
            {
                return WriteResultType.Conflict;
            }

            var pageSize = JsonUtility.GetJsonSize(page, CatalogClient.LegacyEncoder);
            _pages[page.Id] = (new ReadResult<CatalogPage>(page, _tokenProvider.GetETag()), pageSize);
            return WriteResultType.Success;
        }
        finally
        {
            _lock.Release();
        }
    }
}
