using System.ComponentModel;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using JsonLog.NuGetCatalogV3;
using JsonLog.Utility;
using Spectre.Console;
using Spectre.Console.Cli;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Data;

namespace JsonLog.Commands;

public class SimulateNuGetV3CatalogCommand : AsyncCommand<SimulateNuGetV3CatalogCommand.Settings>
{
    private readonly TokenProvider _tokenProvider;

    public class Settings : CommandSettings
    {
        [CommandOption("--destination")]
        [Description("Destination storage type to use. Defaults to memory")]
        public StorageType DestinationStorageType { get; set; } = StorageType.Memory;

        [CommandOption("--source")]
        [Description("Event source type to use. Defaults to random")]
        public EventSourceType EventSourceType { get; set; } = EventSourceType.Random;

        [CommandOption("--event-count")]
        [Description("Number of events to write. Defaults to 15 million for the memory event source.")]
        public long EventCount { get; set; } = -1;

        [CommandOption("--db-path")]
        [Description("Source path for the SQLite database to be read with the database event source. Defaults to commits.db in the current working directory")]
        public string DbPath { get; set; } = "commits.db";
    }

    public enum StorageType
    {
        Memory,
        StorageEmulator,
    }

    public enum EventSourceType
    {
        Random,
        Database,
    }

    public SimulateNuGetV3CatalogCommand(TokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        string baseUrl;
        ICatalogWriterStore store;
        switch (settings.DestinationStorageType)
        {
            case StorageType.Memory:
                baseUrl = $"http://127.0.0.1:10000/devstoreaccount1/catalog0/";
                store = new MemoryCatalogWriterStore(_tokenProvider);
                break;
            case StorageType.StorageEmulator:
                var container = new BlobContainerClient("UseDevelopmentStorage=true", "catalog0");
                baseUrl = $"{container.Uri.AbsoluteUri}/";

                await container.DeleteIfExistsAsync();
                await container.CreateAsync(publicAccessType: PublicAccessType.Blob);

                store = new BlobCatalogWriterStore(container, baseUrl);
                break;
            default:
                throw new NotImplementedException($"Unsupported storage type: {settings.DestinationStorageType}");
        }

        await AnsiConsole
            .Progress()
            .Columns(
            [
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn(),
            ])
            .StartAsync(async ctx =>
            {
                SqliteConnection? connection = null;
                try
                {
                    IEnumerable<CatalogCommit> commits;
                    long eventCount = settings.EventCount;
                    switch (settings.EventSourceType)
                    {
                        case EventSourceType.Random:
                            {
                                if (eventCount < 0)
                                {
                                    eventCount = 15_000_000;
                                }

                                AnsiConsole.MarkupLineInterpolated($"Generating {eventCount} random events.");
                                commits = GenerateRandomCommits(baseUrl, eventCount);
                                break;
                            }
                        case EventSourceType.Database:
                            {
                                AnsiConsole.MarkupLineInterpolated($"Reading SQLite database at {settings.DbPath} for events.");
                                connection = new SqliteConnection($"Data Source={settings.DbPath}");
                                connection.Open();
                                var databaseEventCount = GetEventCount(connection);
                                if (eventCount < 0)
                                {
                                    AnsiConsole.MarkupLineInterpolated($"The database has {databaseEventCount} events. All will used.");
                                    eventCount = databaseEventCount;
                                }
                                else if (eventCount > databaseEventCount)
                                {
                                    AnsiConsole.MarkupLineInterpolated($"[red]Only has {databaseEventCount} event are available in the database. Specify an event count less or equal to that.[/]");
                                    return;
                                }
                                else if (eventCount < databaseEventCount)
                                {
                                    AnsiConsole.MarkupLineInterpolated($"The database has {databaseEventCount} events. Only the first {eventCount} events will be used.");
                                }

                                commits = GetDatabaseCommits(baseUrl, connection, eventCount);
                                break;
                            }
                        default:
                            throw new NotImplementedException($"Unsupported event source type: {settings.EventSourceType}");
                    }

                    var progress = ctx.AddTask("[green]Writing events[/]", maxValue: eventCount);

                    var writer = new CatalogWriter(store);
                    foreach (var commit in commits)
                    {
                        await writer.WriteAsync(commit);
                        progress.Value += commit.Events.Count;
                    }
                }
                finally
                {
                    connection?.Dispose();
                }
            });

        return 0;
    }

    private long GetEventCount(SqliteConnection connection)
    {
        using var countCommand = connection.CreateCommand();
        countCommand.CommandText =
            """
            SELECT SUM(Count)
            FROM CatalogCommits
            """;
        countCommand.CommandType = CommandType.Text;

        return (long)countCommand.ExecuteScalar()!;
    }

    private IEnumerable<CatalogCommit> GetDatabaseCommits(string baseUrl, SqliteConnection connection, long eventCount)
    {
        using var commitsCommand = connection.CreateCommand();
        commitsCommand.CommandText =
            """
            SELECT Id, Timestamp, IsDelete, Count, Items
            FROM CatalogCommits
            ORDER BY Timestamp ASC, Id ASC
            """;
        commitsCommand.CommandType = CommandType.Text;

        using var reader = commitsCommand.ExecuteReader();
        var commitIdBuffer = new byte[16];
        long eventCountSoFar = 0;
        while (reader.Read())
        {
            if (reader.GetBytes(0, 0, commitIdBuffer, 0, 16) != 16)
            {
                throw new InvalidOperationException("Failed to read commit ID.");
            }

            var commitId = new Guid(commitIdBuffer, bigEndian: true).ToString();
            var timestamp = new DateTimeOffset(reader.GetInt64(1), TimeSpan.Zero);
            var isDelete = reader.GetByte(2) switch
            {
                0 => false,
                1 => true,
                _ => throw new InvalidOperationException("Invalid isDelete value."),
            };
            var count = reader.GetInt32(3);
            var leafItems = reader.GetString(4);

            var events = new List<PackageEvent>(count);
            foreach (var leafItem in JsonSerializer.Deserialize<string[][]>(leafItems)!)
            {
                events.Add(new PackageEvent
                {
                    NuGetId = leafItem[0],
                    NuGetVersion = leafItem[1],
                    Type = isDelete ? "nuget:PackageDelete" : "nuget:PackageDetails",
                });
            }

            if (count != events.Count)
            {
                throw new InvalidOperationException($"Commit size mismatch for commit {commitId}: {count} != {events.Count}.");
            }

            var eventsRemaining = eventCount - eventCountSoFar;
            if (eventsRemaining < count)
            {
                var extra = count - (int)eventsRemaining;
                events.RemoveRange((int)eventsRemaining, extra);
            }

            var commit = new CatalogCommit
            {
                BaseUrl = baseUrl,
                Id = commitId,
                CommitTimestamp = timestamp,
                Events = events,
                NuGetLastCreated = timestamp,
                NuGetLastEdited = timestamp,
                NuGetLastDeleted = timestamp,
            };

            eventCountSoFar += events.Count;

            yield return commit;

            if (eventCountSoFar >= eventCount)
            {
                break;
            }
        }
    }

    private IEnumerable<CatalogCommit> GenerateRandomCommits(string baseUrl, long eventCount)
    {
        long eventCountSoFar = 0;
        do
        {
            var eventsRemaining = eventCount - eventCountSoFar;
            var commitEventCount = (int)_tokenProvider.GetRandomNumber(1, Math.Min(20, eventsRemaining) + 1);
            var commit = new CatalogCommit
            {
                BaseUrl = baseUrl,
                Id = _tokenProvider.GetGuidString(),
                CommitTimestamp = _tokenProvider.GetDateTimeOffset(),
                Events = Enumerable
                    .Range(0, commitEventCount)
                    .Select(x => new PackageEvent
                    {
                        NuGetId = _tokenProvider.GetNuGetId(),
                        NuGetVersion = _tokenProvider.GetNuGetVersion(),
                        Type = "nuget:PackageDetails",
                    })
                    .ToList(),
                NuGetLastCreated = _tokenProvider.GetDateTimeOffset(),
                NuGetLastEdited = _tokenProvider.GetDateTimeOffset(),
                NuGetLastDeleted = _tokenProvider.GetDateTimeOffset(),
            };

            eventCountSoFar += commit.Events.Count;

            yield return commit;
        }
        while (eventCountSoFar < eventCount);
    }
}
