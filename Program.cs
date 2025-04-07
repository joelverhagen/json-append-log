using JsonLog.Commands;
using JsonLog.Infrastructure;
using JsonLog.NuGetCatalogV3;
using JsonLog.Utility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;

namespace JsonLog;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
            })
            .ConfigureServices(services =>
            {
                services.AddSingleton<TokenProvider>();
                services.AddTransient<CatalogClient>();
                services
                    .AddHttpClient<CatalogClient>()
                    .AddTransientHttpErrorPolicy(policyBuilder =>
                        policyBuilder.WaitAndRetryAsync(
                            retryCount: 3,
                            retryNumber => TimeSpan.FromMilliseconds(600)));

                services.AddCommandLine(config =>
                {
                    config.AddCommand<SandboxCommand>("sandbox");
                    config.AddCommand<BuildDbCommand>("build-db");
                    config.AddCommand<SimulateNuGetV3CatalogCommand>("simulate-nuget-v3-catalog");
                });
            });

        return await builder.Build().RunAsync(args);
    }
}
