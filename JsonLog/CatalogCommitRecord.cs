namespace JsonLog;

public class CatalogCommitRecord
{
    public required byte[] Id { get; set; }
    public required long Timestamp { get; set; }
    public required bool IsDelete { get; set; }
    public required int Count { get; set; }
    public required string Items { get; set; }
}
