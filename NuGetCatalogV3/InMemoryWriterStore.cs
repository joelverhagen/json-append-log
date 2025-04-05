namespace JsonLog.NuGetCatalogV3;

public class InMemoryWriterStore : IWriterStore
{
    private readonly SemaphoreSlim _lock = new(1);
    private ReadResult<Index>? _index;
    private readonly Dictionary<string, ReadResult<Page>> _pages = new();
    private readonly TokenProvider _tokenProvider;

    public InMemoryWriterStore(TokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    public async Task<ReadResult<Index>?> ReadIndexAsync()
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

    public async Task<WriteResultType> AddIndexAsync(Index index)
    {
        await _lock.WaitAsync();
        try
        {
            if (_index is not null)
            {
                return WriteResultType.Conflict;
            }

            _index = new ReadResult<Index>(index, _tokenProvider.GetETag());
            return WriteResultType.Success;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<WriteResultType> UpdateIndexAsync(Index index, string etag)
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

            _index = new ReadResult<Index>(index, _tokenProvider.GetETag());
            return WriteResultType.Success;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ReadResult<Page>> ReadPageAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            if (!_pages.TryGetValue(id, out var page))
            {
                throw new InvalidOperationException($"Page {id} not found.");
            }

            return page;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<WriteResultType> AddPageAsync(Page page)
    {
        await _lock.WaitAsync();
        try
        {
            if (_pages.ContainsKey(page.Id))
            {
                return WriteResultType.Conflict;
            }

            _pages[page.Id] = new ReadResult<Page>(page, _tokenProvider.GetETag());
            return WriteResultType.Success;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<WriteResultType> UpdatePageAsync(Page page, string etag)
    {
        await _lock.WaitAsync();
        try
        {
            if (!_pages.TryGetValue(page.Id, out var existingPage))
            {
                return WriteResultType.Conflict;
            }

            if (existingPage.ETag != etag)
            {
                return WriteResultType.Conflict;
            }

            _pages[page.Id] = new ReadResult<Page>(page, _tokenProvider.GetETag());
            return WriteResultType.Success;
        }
        finally
        {
            _lock.Release();
        }
    }
}
