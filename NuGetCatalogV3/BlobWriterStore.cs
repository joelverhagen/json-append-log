using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Text.Json;
using Azure;

namespace JsonLog.NuGetCatalogV3;

public class BlobWriterStore : IWriterStore
{
    private readonly BlobContainerClient _containerClient;
    private readonly string _baseUrl;

    public BlobWriterStore(BlobContainerClient containerClient, string baseUrl)
    {
        _containerClient = containerClient;
        _baseUrl = baseUrl;
    }

    public async Task<ReadResult<Index>?> ReadIndexAsync()
    {
        var blobClient = _containerClient.GetBlobClient("index.json");
        try
        {
            using BlobDownloadStreamingResult result = await blobClient.DownloadStreamingAsync();
            var index = await JsonSerializer.DeserializeAsync<Index>(result.Content, Client.LegacyEncoder);
            if (index is null)
            {
                throw new InvalidOperationException("Failed to deserialize index.");
            }

            return new ReadResult<Index>(index, result.Details.ETag.ToString());
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.BlobNotFound)
        {
            return null;
        }
    }

    public async Task<WriteResultType> AddIndexAsync(Index index)
    {
        var blobClient = _containerClient.GetBlobClient("index.json");
        try
        {
            var data = BinaryData.FromObjectAsJson(index, Client.LegacyEncoder);
            await blobClient.UploadAsync(data, options: new BlobUploadOptions { HttpHeaders = new() { ContentType = "application/json" } });
            return WriteResultType.Success;
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.BlobAlreadyExists)
        {
            return WriteResultType.Conflict;
        }
    }

    public async Task<WriteResultType> UpdateIndexAsync(Index index, string etag)
    {
        var blobClient = _containerClient.GetBlobClient("index.json");
        try
        {
            var data = BinaryData.FromObjectAsJson(index, Client.LegacyEncoder);
            await blobClient.UploadAsync(data, new BlobUploadOptions
            {
                Conditions = new BlobRequestConditions { IfMatch = new ETag(etag) },
                HttpHeaders = new() { ContentType = "application/json" },
            });
            return WriteResultType.Success;
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.ConditionNotMet)
        {
            return WriteResultType.Conflict;
        }
    }

    public async Task<ReadResult<Page>> ReadPageAsync(string id)
    {
        var blobClient = _containerClient.GetBlobClient(GetBlobNameFromId(id));
        
        using BlobDownloadStreamingResult result = await blobClient.DownloadStreamingAsync();
        
        var page = await JsonSerializer.DeserializeAsync<Page>(result.Content, Client.LegacyEncoder);
        if (page is null)
        {
            throw new InvalidOperationException("Failed to deserialize page.");
        }

        return new ReadResult<Page>(page, result.Details.ETag.ToString());
    }

    public async Task<WriteResultType> AddPageAsync(Page page)
    {
        var blobClient = _containerClient.GetBlobClient(GetBlobNameFromId(page.Id));
        try
        {
            var data = BinaryData.FromObjectAsJson(page, Client.LegacyEncoder);
            await blobClient.UploadAsync(data, options: new BlobUploadOptions { HttpHeaders = new() { ContentType = "application/json" } });
            return WriteResultType.Success;
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.BlobAlreadyExists)
        {
            return WriteResultType.Conflict;
        }
    }

    public async Task<WriteResultType> UpdatePageAsync(Page page, string etag)
    {
        var blobClient = _containerClient.GetBlobClient(GetBlobNameFromId(page.Id));
        try
        {
            var data = BinaryData.FromObjectAsJson(page, Client.LegacyEncoder);
            await blobClient.UploadAsync(data, new BlobUploadOptions
            {
                Conditions = new BlobRequestConditions { IfMatch = new ETag(etag) },
                HttpHeaders = new() { ContentType = "application/json" },
            });
            return WriteResultType.Success;
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.ConditionNotMet)
        {
            return WriteResultType.Conflict;
        }
    }

    private string GetBlobNameFromId(string id)
    {
        return id.Substring(_baseUrl.Length);
    }
}