using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JsonLog.NuGetCatalogV3;

public class DateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateTimeOffset.Parse(reader.GetString()!, CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new JsonException("Only UTC timestamps are supported.");
        }

        writer.WriteStringValue(value.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'FFFFFFF'Z'", CultureInfo.InvariantCulture));
    }
}
