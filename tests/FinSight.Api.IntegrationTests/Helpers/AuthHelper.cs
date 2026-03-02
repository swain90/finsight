using System.Net.Http.Json;
using System.Text.Json;

namespace FinSight.Api.IntegrationTests.Helpers;

/// <summary>Shared helper to register and login test users, returning a bearer token.</summary>
public static class AuthHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<string> GetAccessTokenAsync(
        HttpClient client,
        string email = "test@finsight.dev",
        string password = "Test@Password1!")
    {
        // Register
        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password,
            firstName = "Test",
            lastName = "User"
        });

        // If already registered, just login
        if (!registerResponse.IsSuccessStatusCode)
        {
            return await LoginAsync(client, email, password);
        }

        var registerData = await registerResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return registerData.GetProperty("accessToken").GetString()!;
    }

    public static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return data.GetProperty("accessToken").GetString()!;
    }

    public static void SetBearerToken(this HttpClient client, string token)
        => client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
}
