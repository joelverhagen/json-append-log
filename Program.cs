using JsonLog.NuGetCatalogV3;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var tokenProvider = new TokenProvider();
        var store = new InMemoryWriterStore(tokenProvider);
        var writer = new Writer(store);
        var commit = new Commit
        {
            BaseUrl = "https://api.nuget.org/v3/catalog0/",
            Id = tokenProvider.GetGuidString(),
            CommitTimestamp = tokenProvider.GetDateTimeOffset(),
            Events =
            [
                new PackageEvent
                {
                    NuGetId = tokenProvider.GetNuGetId(),
                    NuGetVersion = tokenProvider.GetNuGetVersion(),
                    Type = "nuget:PackageDetails",
                },
            ],
            NuGetLastCreated = tokenProvider.GetDateTimeOffset(),
            NuGetLastEdited = tokenProvider.GetDateTimeOffset(),
            NuGetLastDeleted = tokenProvider.GetDateTimeOffset(),
        };
        await writer.WriteAsync(commit);
    }
}
