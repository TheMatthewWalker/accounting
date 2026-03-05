using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AccountingApp.Tests.Helpers;

public static class AuthHelper
{
    /// <summary>
    /// Registers a user and returns their JWT access token.
    /// Uses a unique email suffix to avoid duplicate conflicts.
    /// </summary>
    public static async Task<string> RegisterAndGetTokenAsync(
        HttpClient client,
        string emailPrefix = "testuser",
        string password = "Password123!")
    {
        var email = $"{emailPrefix}_{Guid.NewGuid():N}@example.com";

        var registerData = new
        {
            email,
            password,
            firstName = "Test",
            lastName = "User"
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", registerData);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        return content.GetProperty("accessToken").GetString()!;
    }

    /// <summary>
    /// Sets a Bearer token on the client's default request headers.
    /// </summary>
    public static void SetBearerToken(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Registers a user, sets their token on the client, and creates an organisation.
    /// Returns the organisation ID.
    /// </summary>
    public static async Task<Guid> SetupWithOrganisationAsync(
        HttpClient client,
        string orgName = "Test Organisation")
    {
        var token = await RegisterAndGetTokenAsync(client);
        SetBearerToken(client, token);

        var orgData = new
        {
            name = orgName,
            description = "Test organisation"
        };

        var orgResponse = await client.PostAsJsonAsync("/api/organisations", orgData);
        orgResponse.EnsureSuccessStatusCode();

        var orgContent = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
        return orgContent.GetProperty("id").GetGuid();
    }
}
