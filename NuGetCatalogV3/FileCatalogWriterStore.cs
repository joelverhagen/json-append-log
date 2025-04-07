using System.Text.Json;

namespace JsonLog.NuGetCatalogV3;

public class FileCatalogWriterStore : ICatalogWriterStore
{
    private readonly SemaphoreSlim _lock = new(1);
    private readonly string _baseUrl;
    private readonly string _baseDirectory;

    public FileCatalogWriterStore(string baseUrl, string baseDirectory)
    {
        _baseUrl = baseUrl;
        _baseDirectory = baseDirectory;
    }

    public async Task<ReadResult<CatalogIndex>?> ReadIndexAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var filePath = Path.Combine(_baseDirectory, "index.json");
            try
            {
                using var stream = File.OpenRead(filePath);
                var index = await JsonSerializer.DeserializeAsync<CatalogIndex>(stream);
                if (index is null)
                {
                    throw new InvalidOperationException("Failed to deserialize index.");
                }

                return new ReadResult<CatalogIndex>(index, GetETag(filePath));
            }
            catch (FileNotFoundException)
            {
                return null;
            }
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
            var filePath = Path.Combine(_baseDirectory, "index.json");
            using var stream = new FileStream(filePath, FileMode.CreateNew);
            await JsonSerializer.SerializeAsync(stream, index);
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
            var filePath = Path.Combine(_baseDirectory, "index.json");
            if (GetETag(filePath) != etag)
            {
                throw new InvalidOperationException("ETag mismatch.");
            }

            using var stream = new FileStream(filePath, FileMode.Create);
            await JsonSerializer.SerializeAsync(stream, index);
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
            var filePath = Path.Combine(_baseDirectory, GetFileNameFromId(id));
            try
            {
                using var stream = File.OpenRead(filePath);
                var page = await JsonSerializer.DeserializeAsync<CatalogPage>(stream);
                if (page is null)
                {
                    throw new InvalidOperationException("Failed to deserialize page.");
                }

                return new ReadResult<CatalogPage>(page, GetETag(filePath));
            }
            catch (FileNotFoundException)
            {
                throw new InvalidOperationException($"Page {id} not found.");
            }
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
            var filePath = Path.Combine(_baseDirectory, GetFileNameFromId(page.Id));
            using var stream = new FileStream(filePath, FileMode.CreateNew);
            await JsonSerializer.SerializeAsync(stream, page);
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
            var filePath = Path.Combine(_baseDirectory, GetFileNameFromId(page.Id));
            if (GetETag(filePath) != etag)
            {
                throw new InvalidOperationException("ETag mismatch.");
            }

            using var stream = new FileStream(filePath, FileMode.Create);
            await JsonSerializer.SerializeAsync(stream, page);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string GetETag(string path)
    {
        return $"\"{File.GetLastWriteTimeUtc(path).Ticks}\"";
    }

    private string GetFileNameFromId(string id)
    {
        if (!id.StartsWith(_baseUrl))
        {
            throw new ArgumentException($"ID {id} must start with {_baseUrl}", nameof(id));
        }

        var fileName = id.Substring(_baseUrl.Length);
        if (Path.DirectorySeparatorChar != '/')
        {
            fileName = fileName.Replace('/', Path.DirectorySeparatorChar);
        }

        return fileName;
    }
}
