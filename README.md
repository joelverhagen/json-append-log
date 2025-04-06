# JSON append log

This is an experimental repository. My goal is to try a new design for the
[NuGet V3 catalog protocol](https://learn.microsoft.com/en-us/nuget/api/catalog-resource) ([how-to](https://learn.microsoft.com/en-us/nuget/guides/api/query-for-all-published-packages)).
Currently it has an `catalog0/index.json` file that is growing infinitely. The growth rate is not too
bad but the it's more than 3 MB of JSON right now.

```powershell
$index = "https://api.nuget.org/v3/catalog0/index.json"
$response = Invoke-WebRequest -Method HEAD $index
$mb = $response.Headers["Content-Length"] / (1024.0 * 1024)
Write-Host "The catalog index is $mb MB."
```

Related issues on NuGet/NuGetGallery:
- (done) [Increase page size of the V3 catalog](https://github.com/NuGet/NuGetGallery/issues/9146) (it was increased to 2750)
- (not done) [Add catalog tail resource](https://github.com/NuGet/NuGetGallery/issues/7787)

Ideally the design works well on a blob storage (e.g. Azure Blob Storage) with CDN in front of it, to utilize
caching to the best of its ability. The current design is pretty good for that but the root node (index)
grows in an unbounded way. The completed page nodes have a long `Cache-Control` but the latest page and the index
have no caching at all.

A tail resource might be the best solution since it would be additive to the current
protocol.

I was originally thinking a B-tree was the right plan but that is optimized for accessing
any of the keys. I imagine users will either want the latest entries or want to start from
the beginning. Accessing somewhere in the middle of the event log seems less important.

I think it would be cool to generate a JSON append log for other package ecosystems using the
same schema. For example, the npm replication API could be projected into this.

## Tools

Right now the source is a CLI tool with two commands.

`build-db` - creates a SQLite DB with all of the catalog commits. This is enough information to
reproduce the index and page nodes of the catalog. This is [run in GitHub Actions](https://github.com/joelverhagen/json-append-log/actions/workflows/nuget-catalog-commits.yml)
so you can easily download the SQLite DB from the artifacts.

`simulate-nuget-v3-catalog` - writes the catalog structure to memory or Azure Blob Storage emulator
(such as Azurite).

## Build your own catalog

1. Download the latest `nuget-prod-catalog` from the latest [Build NuGet V3 Catalog DB](https://github.com/joelverhagen/json-append-log/actions/workflows/nuget-catalog-commits.yml).
1. Extract the .zip. The file is over 1 GB.
1. Install [Azurite](https://marketplace.visualstudio.com/items?itemName=Azurite.azurite) in VS Code.
1. Start the Blob Storage emulator.
1. Run the following command in the root of this repository. Fix up the `--db-path` option to be the location where you extracted the DB in step 2.
   ```powershell
   dotnet run --configuration Release -- `       simulate-nuget-v3-catalog `       --source Database --destination StorageEmulator `
       --db-path path/to/nuget-prod-catalog.db `
       --event-count 10000
   ```
1. Go to `http://127.0.0.1:10000/devstoreaccount1/catalog0/index.json`.

The first 10,000 catalog events from NuGet.org PROD will be written to your storage emulator.

The index and pages are stored in your blob storage. The leaf URLs point to `api.nuget.org`.
