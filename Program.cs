using JsonLog.NuGetCatalogV3;

internal class Program
{
    private static async Task Main(string[] args)
    {
        using var httpClient = new HttpClient();
        var client = new Client(httpClient, validateRoundTrip: true);
        // var index = await client.ReadIndexAsync("https://apiint.nugettest.org/v3/catalog0/index.json");
        var page = await client.ReadPageAsync("https://apiint.nugettest.org/v3/catalog0/page2036.json");
    }
}
