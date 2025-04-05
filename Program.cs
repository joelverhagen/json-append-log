using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using JsonLog.NuGetCatalogV3;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var tokenProvider = new TokenProvider();

        string baseUrl;
        IWriterStore store;
        if (args.FirstOrDefault() == "blobs")
        {
            var container = new BlobContainerClient("UseDevelopmentStorage=true", "catalog0");
            baseUrl = $"{container.Uri.AbsoluteUri}/";

            await container.DeleteIfExistsAsync();
            await container.CreateAsync(publicAccessType: PublicAccessType.Blob);

            store = new BlobWriterStore(container, baseUrl);
        }
        else
        {
            baseUrl = $"http://127.0.0.1:10000/devstoreaccount1/catalog0/";
            store = new InMemoryWriterStore(tokenProvider);
        }

        var writer = new Writer(store);

        int eventCount = 0;
        int passedMillion = 0;
        do
        {
            var commit = new Commit
            {
                BaseUrl = baseUrl,
                Id = tokenProvider.GetGuidString(),
                CommitTimestamp = tokenProvider.GetDateTimeOffset(),
                Events = Enumerable
                    .Range(0, tokenProvider.GetRandomNumber(1, 21))
                    .Select(x => new PackageEvent
                    {
                        NuGetId = tokenProvider.GetNuGetId(),
                        NuGetVersion = tokenProvider.GetNuGetVersion(),
                        Type = "nuget:PackageDetails",
                    })
                    .ToList(),
                NuGetLastCreated = tokenProvider.GetDateTimeOffset(),
                NuGetLastEdited = tokenProvider.GetDateTimeOffset(),
                NuGetLastDeleted = tokenProvider.GetDateTimeOffset(),
            };
            // Console.WriteLine($"Writing commit {commit.Id}...");

            await writer.WriteAsync(commit);

            eventCount += commit.Events.Count;
            if (eventCount > passedMillion)
            {
                Console.WriteLine($"Wrote {eventCount} events");
                passedMillion += 1_000_000;
            }
        }
        while (eventCount < 15_000_000);
    }
}
