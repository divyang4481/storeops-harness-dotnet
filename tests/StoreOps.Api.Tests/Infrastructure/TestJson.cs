using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StoreOps.Api.Tests.Infrastructure;

public static class TestJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var value = await response.Content.ReadFromJsonAsync<T>(Options);
        return value ?? throw new InvalidOperationException($"Response body could not be read as {typeof(T).Name}.");
    }

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.PostAsJsonAsync(url, body, Options);
    }

    public static Task<HttpResponseMessage> PatchJsonAsync<T>(this HttpClient client, string url, T body)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.PatchAsJsonAsync(url, body, Options);
    }

    public static string UniqueStoreId() => $"ST-T-{Guid.NewGuid():N}";

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
