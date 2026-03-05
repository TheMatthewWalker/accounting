using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using AccountingApp.Tests.Helpers;
using Xunit;

namespace AccountingApp.Tests;

public class AuthControllerTests : IClassFixture<AccountingWebApplicationFactory>
{
    private readonly AccountingWebApplicationFactory _factory;

    public AuthControllerTests(AccountingWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ==================== Registration Tests ====================

    [Fact]
    public async Task Register_WithValidData_Returns200WithToken()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"valid_{Guid.NewGuid():N}@example.com",
            password = "Password123!",
            firstName = "John",
            lastName = "Doe"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("success").GetBoolean().Should().BeTrue();
        content.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_WithInvalidEmail_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "not-an-email",
            password = "Password123!",
            firstName = "John",
            lastName = "Doe"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithShortPassword_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"test_{Guid.NewGuid():N}@example.com",
            password = "Ab1!",
            firstName = "John",
            lastName = "Doe"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithMissingFirstName_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"test_{Guid.NewGuid():N}@example.com",
            password = "Password123!",
            lastName = "Doe"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409()
    {
        var client = _factory.CreateClient();
        var email = $"duplicate_{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password123!",
            firstName = "John",
            lastName = "Doe"
        });

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password123!",
            firstName = "Jane",
            lastName = "Doe"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("error").GetProperty("code").GetString().Should().Be("DUPLICATE_RESOURCE");
    }

    // ==================== Login Tests ====================

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithToken()
    {
        var client = _factory.CreateClient();
        var email = $"login_{Guid.NewGuid():N}@example.com";
        const string password = "Password123!";

        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password,
            firstName = "Test",
            lastName = "User"
        });

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("success").GetBoolean().Should().BeTrue();
        content.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = _factory.CreateClient();
        var email = $"wrongpw_{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password123!",
            firstName = "Test",
            lastName = "User"
        });

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "WrongPassword999!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("error").GetProperty("code").GetString().Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Login_WithNonExistentEmail_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "nobody@nowhere.com",
            password = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ==================== Protected Endpoint Tests ====================

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/organisations");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidToken_Returns200()
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.RegisterAndGetTokenAsync(client);
        AuthHelper.SetBearerToken(client, token);

        var response = await client.GetAsync("/api/organisations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithInvalidToken_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "this.is.not.a.valid.token");

        var response = await client.GetAsync("/api/organisations");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
