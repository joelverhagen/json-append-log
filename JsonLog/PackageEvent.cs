namespace JsonLog;

public class PackageEvent
{
    public required string NuGetId { get; set; }
    public required string NuGetVersion { get; set; }
    public required string Type { get; set; }
}
