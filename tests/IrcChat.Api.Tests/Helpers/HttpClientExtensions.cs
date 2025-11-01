// tests/IrcChat.Api.Tests/Helpers/HttpClientExtensions.cs
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace IrcChat.Api.Tests.Helpers;

public static class HttpClientExtensions
{
    public static void SetBearerToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    public static async Task<T?> GetJsonAsync<T>(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }

    public static async Task<HttpResponseMessage> PostJsonAsync<T>(
        this HttpClient client,
        string url,
        T data) => await client.PostAsJsonAsync(url, data);
}