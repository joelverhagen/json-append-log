using JsonLog.NuGetCatalogV3;
using JsonLog.Utility;
using Spectre.Console.Cli;

namespace JsonLog.Commands;

public class SandboxCommand : AsyncCommand<SandboxCommand.Settings>
{
    private readonly CatalogClient _client;

    public class Settings : CommandSettings
    {
    }

    public SandboxCommand(CatalogClient client)
    {
        _client = client;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var index = await _client.ReadIndexAsync("https://apiint.nugettest.org/v3/catalog0/index.json");
        var size = JsonUtility.GetJsonSize(index, CatalogClient.LegacyEncoder);

        return 0;
    }
}
