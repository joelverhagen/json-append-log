using System.Text.Encodings.Web;
using System.Text.Json;
using JsonLog.Utility;

namespace JsonLog.NuGetCatalogV3;

public class CatalogClient
{
    public static JsonSerializerOptions LegacyEncoder { get; } = new JsonSerializerOptions
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    public static JsonSerializerOptions LegacyEncoderIndented { get; } = new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly HttpClient _httpClient;
    private readonly bool _validateRoundTrip;

    public CatalogClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _validateRoundTrip = false;
    }

    public async Task<CatalogIndex> ReadIndexAsync(string url)
    {
        return await ReadAsync<CatalogIndex>(url, LegacyEncoder);
    }

    public async Task<CatalogPage> ReadPageAsync(string url)
    {
        return await ReadAsync<CatalogPage>(url, LegacyEncoder);
    }

    private async Task<T> ReadAsync<T>(string url, JsonSerializerOptions options)
    {
        if (_validateRoundTrip)
        {
            var originalJson = await _httpClient.GetStringAsync(url);
            var deserialized = JsonSerializer.Deserialize<T>(originalJson);
            if (deserialized is null)
            {
                throw new JsonException("Deserialized model should not be null.");
            }

            JsonUtility.VerifyRoundTrip(originalJson, deserialized, options);

            return deserialized;
        }
        else
        {
            using var stream = await _httpClient.GetStreamAsync(url);
            var deserialized = await JsonSerializer.DeserializeAsync<T>(stream);
            if (deserialized is null)
            {
                throw new JsonException("Deserialized model should not be null.");
            }

            return deserialized;
        }
    }
}
