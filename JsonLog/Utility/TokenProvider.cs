namespace JsonLog.Utility;

public class TokenProvider
{
    private int _next;

    public string GetETag()
    {
        var next = Interlocked.Increment(ref _next);
        return $"\"{next}\"";
    }

    public string GetGuidString()
    {
        var next = Interlocked.Increment(ref _next);
        var bytes = BitConverter.GetBytes(next);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        bytes = new byte[12].Concat(bytes).ToArray();

        return new Guid(bytes, bigEndian: true).ToString();
    }

    public DateTimeOffset GetDateTimeOffset()
    {
        var next = Interlocked.Increment(ref _next);
        return new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(next);
    }

    public string GetNuGetId()
    {
        var next = Interlocked.Increment(ref _next);
        return $"Package{next}";
    }

    public long GetRandomNumber(long minInclusive, long maxExclusive)
    {
        var seed = Interlocked.Increment(ref _next);
        return new Random(seed).NextInt64(minInclusive, maxExclusive);
    }

    public string GetNuGetVersion()
    {
        var next = Interlocked.Increment(ref _next);
        return $"1.0.{next}";
    }
}
