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

    public async Task AddIndexAsync(CatalogIndex index)
    {
        await _lock.WaitAsync();
        try
        {
            if (_index is not null)
            {
                throw new InvalidOperationException("Index already exists.");
            }

            _index = new ReadResult<CatalogIndex>(index, _tokenProvider.GetETag());
            _indexSize = JsonUtility.GetJsonSize(index, CatalogClient.LegacyEncoder);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateIndexAsync(CatalogIndex index, string etag)
    {
        await _lock.WaitAsync();
        try
        {
            if (_index is null)
            {
                throw new InvalidOperationException("Index does not exist.");
            }

            if (_index.ETag != etag)
            {
                throw new InvalidOperationException("ETag mismatch.");
            }

            _index = new ReadResult<CatalogIndex>(index, _tokenProvider.GetETag());
            _indexSize = JsonUtility.GetJsonSize(index, CatalogClient.LegacyEncoder);
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

    public async Task AddPageAsync(CatalogPage page)
    {
        await _lock.WaitAsync();
        try
        {
            if (_pages.ContainsKey(page.Id))
            {
                throw new InvalidOperationException($"Page {page.Id} already exists.");
            }

            var pageSize = JsonUtility.GetJsonSize(page, CatalogClient.LegacyEncoder);
            _pages[page.Id] = (new ReadResult<CatalogPage>(page, _tokenProvider.GetETag()), pageSize);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdatePageAsync(CatalogPage page, string etag)
    {
        await _lock.WaitAsync();
        try
        {
            if (!_pages.TryGetValue(page.Id, out var info))
            {
                throw new InvalidOperationException($"Page {page.Id} not found.");
            }

            if (info.Result.ETag != etag)
            {
                throw new InvalidOperationException("ETag mismatch.");
            }

            var pageSize = JsonUtility.GetJsonSize(page, CatalogClient.LegacyEncoder);
            _pages[page.Id] = (new ReadResult<CatalogPage>(page, _tokenProvider.GetETag()), pageSize);
        }
        finally
        {
            _lock.Release();
        }
    }
}
