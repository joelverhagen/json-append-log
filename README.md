# JSON append log

This is an experimental repository. My goal is to try a new design for the NuGet V3 catalog protocol.
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
grows in an unbounded way.

A tail resource might be the best solution since it would be additive to the current
protocol.

I was originally thinking a B-tree was the right plan but that is optimized for accessing
any of the keys. I imagine users will either want the latest entries or want to start from
the beginning. Accessing somewhere in the middle of the event log seems less important.

## Tools

Right now the source is a CLI tool with two commands.

`build-db` - creates a SQLite DB with all of the catalog commits. This is enough information to
reproduce the index and page nodes of the catalog.

`simulate-nuget-v3-catalog` - writes the catalog structure to memory or Azure Blob Storage emulator
(such as Azurite).
