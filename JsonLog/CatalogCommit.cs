namespace JsonLog;

public class CatalogCommit
{
    public required string Id { get; set; }
    public required DateTimeOffset CommitTimestamp { get; set; }
    public required List<PackageEvent> Events { get; set; }    
    public required DateTimeOffset NuGetLastCreated { get; set; }
    public required DateTimeOffset NuGetLastDeleted { get; set; }
    public required DateTimeOffset NuGetLastEdited { get; set; }
}
