using System.Text.Encodings.Web;
using System.Text.Json;

namespace JsonLog.NuGetCatalogV3;

public class Client
{
    public static JsonSerializerOptions LegacyEncoder => new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly HttpClient _httpClient;
    private readonly bool _validateRoundTrip;

    public Client(HttpClient httpClient, bool validateRoundTrip)
    {
        _httpClient = httpClient;
        _validateRoundTrip = validateRoundTrip;
    }

    public async Task<Index> ReadIndexAsync(string url)
    {
        return await ReadAsync<Index>(url, LegacyEncoder);
    }

    public async Task<Page> ReadPageAsync(string url)
    {
        return await ReadAsync<Page>(url, LegacyEncoder);
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
