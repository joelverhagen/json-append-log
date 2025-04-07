using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Text.Json;
using Azure;

namespace JsonLog.NuGetCatalogV3;

public class BlobCatalogWriterStore : ICatalogWriterStore
{
    private readonly BlobContainerClient _containerClient;
    private readonly string _baseUrl;

    public BlobCatalogWriterStore(BlobContainerClient containerClient, string baseUrl)
    {
        _containerClient = containerClient;
        _baseUrl = baseUrl;
    }

    public async Task<ReadResult<CatalogIndex>?> ReadIndexAsync()
    {
        var blobClient = _containerClient.GetBlobClient("index.json");
        try
        {
            using BlobDownloadStreamingResult result = await blobClient.DownloadStreamingAsync();
            var index = await JsonSerializer.DeserializeAsync<CatalogIndex>(result.Content, CatalogClient.LegacyEncoder);
            if (index is null)
            {
                throw new InvalidOperationException("Failed to deserialize index.");
            }

            return new ReadResult<CatalogIndex>(index, result.Details.ETag.ToString());
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.BlobNotFound)
        {
            return null;
        }
    }

    public async Task AddIndexAsync(CatalogIndex index)
    {
        var blobClient = _containerClient.GetBlobClient("index.json");
        var data = BinaryData.FromObjectAsJson(index, CatalogClient.LegacyEncoder);
        await blobClient.UploadAsync(data, options: new BlobUploadOptions { HttpHeaders = new() { ContentType = "application/json" } });
    }

    public async Task UpdateIndexAsync(CatalogIndex index, string etag)
    {
        var blobClient = _containerClient.GetBlobClient("index.json");
        var data = BinaryData.FromObjectAsJson(index, CatalogClient.LegacyEncoder);
        await blobClient.UploadAsync(data, new BlobUploadOptions
        {
            Conditions = new BlobRequestConditions { IfMatch = new ETag(etag) },
            HttpHeaders = new() { ContentType = "application/json" },
        });
    }

    public async Task<ReadResult<CatalogPage>> ReadPageAsync(string id)
    {
        var blobClient = _containerClient.GetBlobClient(GetBlobNameFromId(id));
        
        using BlobDownloadStreamingResult result = await blobClient.DownloadStreamingAsync();
        
        var page = await JsonSerializer.DeserializeAsync<CatalogPage>(result.Content, CatalogClient.LegacyEncoder);
        if (page is null)
        {
            throw new InvalidOperationException("Failed to deserialize page.");
        }

        return new ReadResult<CatalogPage>(page, result.Details.ETag.ToString());
    }

    public async Task AddPageAsync(CatalogPage page)
    {
        var blobClient = _containerClient.GetBlobClient(GetBlobNameFromId(page.Id));
        var data = BinaryData.FromObjectAsJson(page, CatalogClient.LegacyEncoder);
        await blobClient.UploadAsync(data, options: new BlobUploadOptions { HttpHeaders = new() { ContentType = "application/json" } });
    }

    public async Task UpdatePageAsync(CatalogPage page, string etag)
    {
        var blobClient = _containerClient.GetBlobClient(GetBlobNameFromId(page.Id));
        var data = BinaryData.FromObjectAsJson(page, CatalogClient.LegacyEncoder);
        await blobClient.UploadAsync(data, new BlobUploadOptions
        {
            Conditions = new BlobRequestConditions { IfMatch = new ETag(etag) },
            HttpHeaders = new() { ContentType = "application/json" },
        });
    }

    private string GetBlobNameFromId(string id)
    {
        if (!id.StartsWith(_baseUrl))
        {
            throw new ArgumentException($"ID {id} must start with {_baseUrl}", nameof(id));
        }

        return id.Substring(_baseUrl.Length);
    }
}
