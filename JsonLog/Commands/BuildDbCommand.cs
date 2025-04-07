using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Channels;
using JsonLog.NuGetCatalogV3;
using Microsoft.Data.Sqlite;
using Spectre.Console;
using Spectre.Console.Cli;

namespace JsonLog.Commands;

public class BuildDbCommand : AsyncCommand<BuildDbCommand.Settings>
{
    private readonly CatalogClient _client;
    private int _downloadedCount = 0;
    private int _persistedCount = 0;

    public class Settings : CommandSettings
    {
        [CommandOption("--db-path")]
        [Description("Destination path for the SQLite database that will be created. Defaults to commits.db in the current working directory")]
        public string DbPath { get; set; } = "commits.db";

        [CommandOption("--catalog-index")]
        [Description("Catalog index URL to use. Defaults to NuGet.org production environment")]
        public string CatalogIndexUrl { get; set; } = "https://api.nuget.org/v3/catalog0/index.json";
    }

    public BuildDbCommand(CatalogClient client)
    {
        _client = client;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var fullPath = Path.GetFullPath(settings.DbPath);
        AnsiConsole.MarkupLineInterpolated($"Working with SQLite database at {fullPath}.");

        if (File.Exists(fullPath))
        {
            if (!AnsiConsole.Confirm($"Delete existing DB?"))
            {
                AnsiConsole.MarkupLine("[red]Aborting, since the DB file already exists.[/]");
                return 1;
            }

            File.Delete(fullPath);
        }

        var dir = Path.GetDirectoryName(fullPath);
        if (dir is not null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var connection = new SqliteConnection($"Data Source={fullPath}");
        connection.Open();
        PrepareDatabase(connection);

        var index = await AnsiConsole
            .Status()
            .StartAsync("Downloading the catalog index...", async ctx =>
            {
                var index = await _client.ReadIndexAsync(settings.CatalogIndexUrl);
                AnsiConsole.MarkupLineInterpolated($"The catalog index has {index.Items.Count} pages.");
                return index;
            });

        var channel = Channel.CreateBounded<List<CatalogCommitRecord>>(new BoundedChannelOptions(capacity: 2000)
        {
            SingleReader = true,
            SingleWriter = false,
        });

        AnsiConsole.MarkupLineInterpolated($"The catalog pages will now be downloaded, grouped into commits, and loaded into the DB.");
        var downloadTask = DownloadAsync(index, channel.Writer);
        var persistTask = PersistCommitsAsync(connection, channel.Reader);

        await AnsiConsole
            .Progress()
            .Columns(
            [
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn(),
            ])
            .StartAsync(async ctx =>
            {
                // Define tasks
                var downloadProgress = ctx.AddTask("[green]Downloading pages[/]", maxValue: index.Items.Count);
                var persistProgress = ctx.AddTask("[green]Persisting commits[/]", maxValue: index.Items.Count);

                while (!downloadTask.IsCompleted || !persistTask.IsCompleted)
                {
                    await Task.Delay(250);

                    downloadProgress.Value = _downloadedCount;
                    persistProgress.Value = _persistedCount;
                }
            });

        return 0;
    }

    private async Task DownloadAsync(CatalogIndex index, ChannelWriter<List<CatalogCommitRecord>> channelWriter)
    {
        var pageItems = new ConcurrentQueue<CatalogPageItem>(index.Items.OrderBy(x => x.CommitTimestamp));

        var downloadTask = Task.WhenAll(Enumerable
            .Range(0, 8)
            .Select(x => DownloadWorkerAsync(pageItems, channelWriter)));

        await downloadTask;

        channelWriter.Complete();
    }

    private async Task DownloadWorkerAsync(ConcurrentQueue<CatalogPageItem> pageItems, ChannelWriter<List<CatalogCommitRecord>> channelWriter)
    {
        await Task.Yield();

        while (pageItems.TryDequeue(out var pageItem))
        {
            try
            {
                var page = await _client.ReadPageAsync(pageItem.Id);
                var pageCommits = new List<CatalogCommitRecord>();
                foreach (var commit in page.Items.GroupBy(x => x.CommitId))
                {
                    var commitTimestamp = commit.Select(x => x.CommitTimestamp).Distinct().Single();
                    var type = commit.Select(x => x.Type).Distinct().Single();
                    var items = commit
                        .Select(x => new object[] { x.NuGetId, x.NuGetVersion })
                        .ToList();

                    pageCommits.Add(new CatalogCommitRecord
                    {
                        Id = new Guid(commit.Key).ToByteArray(bigEndian: true),
                        Timestamp = commitTimestamp.UtcTicks,
                        IsDelete = type switch
                        {
                            "nuget:PackageDelete" => true,
                            "nuget:PackageDetails" => false,
                            _ => throw new NotImplementedException(),
                        },
                        Count = items.Count,
                        Items = JsonSerializer.Serialize(items),
                    });
                }

                await channelWriter.WriteAsync(pageCommits);

                Interlocked.Increment(ref _downloadedCount);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Error reading page {pageItem.Id}: {ex.Message}[/]");
                continue;
            }
        }
    }

    private async Task PersistCommitsAsync(SqliteConnection connection, ChannelReader<List<CatalogCommitRecord>> channelReader)
    {
        await Task.Yield();

        try
        {
            var transaction = connection.BeginTransaction();
            var (insert, id, timestamp, isDelete, count, items) = CreateInsertCommand(connection);
            var insertCount = 0;

            while (await channelReader.WaitToReadAsync())
            {
                while (channelReader.TryRead(out var pageCommits))
                {
                    foreach (var commit in pageCommits)
                    {
                        id.Value = commit.Id;
                        timestamp.Value = commit.Timestamp;
                        isDelete.Value = commit.IsDelete ? 1 : 0;
                        count.Value = commit.Count;
                        items.Value = commit.Items;
                        insert.ExecuteNonQuery();
                        insertCount++;

                        if (insertCount > 250_000)
                        {
                            transaction.Commit();
                            insert.Dispose();
                            transaction.Dispose();
                            transaction = connection.BeginTransaction();
                            (insert, id, timestamp, isDelete, count, items) = CreateInsertCommand(connection);
                            insertCount = 0;
                        }
                    }

                    Interlocked.Increment(ref _persistedCount);
                }
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Error writing to database: {ex.Message}[/]");
        }
    }

    private static (SqliteCommand command, SqliteParameter id, SqliteParameter timestamp, SqliteParameter isDelete, SqliteParameter count, SqliteParameter items) CreateInsertCommand(SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO CatalogCommits (Id, Timestamp, IsDelete, Count, Items)
            VALUES ($id, $timestamp, $isDelete, $count, $items)
            """;
        var id = new SqliteParameter("$id", SqliteType.Blob);
        var timestamp = new SqliteParameter("$timestamp", SqliteType.Integer);
        var isDelete = new SqliteParameter("$isDelete", SqliteType.Integer);
        var count = new SqliteParameter("$count", SqliteType.Integer);
        var items = new SqliteParameter("items", SqliteType.Text);

        command.Parameters.Add(id);
        command.Parameters.Add(timestamp);
        command.Parameters.Add(isDelete);
        command.Parameters.Add(count);
        command.Parameters.Add(items);

        return (command, id, timestamp, isDelete, count, items);
    }

    private static void PrepareDatabase(SqliteConnection connection)
    {
        var createTable = connection.CreateCommand();
        createTable.CommandText =
            """
            CREATE TABLE CatalogCommits (
                Id BLOB NOT NULL CONSTRAINT PK_CatalogCommits PRIMARY KEY,
                Timestamp INTEGER NOT NULL,
                IsDelete INTEGER NOT NULL,
                Count INTEGER NOT NULL,
                Items TEXT NOT NULL
            )
            """;
        createTable.ExecuteNonQuery();

        var createIndex = connection.CreateCommand();
        createIndex.CommandText =
            """
            CREATE INDEX IX_CatalogCommits_Timestamp ON CatalogCommits (Timestamp)
            """;
        createIndex.ExecuteNonQuery();
    }
}
