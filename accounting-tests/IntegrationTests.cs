using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using AccountingApp.Data;
using AccountingApp.Models;
using Xunit;

namespace AccountingApp.Tests;

public class IntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public IntegrationTests(WebApplicationFactory<Program> factory)
    {
        // customize factory to use in-memory database
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb");
                });
            });
        });
    }

    [Fact]
    public async Task Registration_Login_Workflow()
    {
        var client = _factory.CreateClient();

        var registerData = new
        {
            email = "test@example.com",
            password = "Password123!",
            firstName = "Test",
            lastName = "User"
        };

        var regResponse = await client.PostAsJsonAsync("/api/auth/register", registerData);
        regResponse.EnsureSuccessStatusCode();
        var regContent = await regResponse.Content.ReadFromJsonAsync<JsonElement>();
        regContent.GetProperty("success").GetBoolean().Should().BeTrue();
        regContent.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();

        var loginData = new
        {
            email = "test@example.com",
            password = "Password123!"
        };
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginData);
        loginResponse.EnsureSuccessStatusCode();
        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        loginContent.GetProperty("success").GetBoolean().Should().BeTrue();
        loginContent.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Organisation_Create_And_Access()
    {
        var client = _factory.CreateClient();

        // register/login to obtain token
        var registerData = new
        {
            email = "orgtest@example.com",
            password = "Password123!",
            firstName = "Org",
            lastName = "Test"
        };
        var regResp = await client.PostAsJsonAsync("/api/auth/register", registerData);
        regResp.EnsureSuccessStatusCode();
        var regContent = await regResp.Content.ReadFromJsonAsync<JsonElement>();
        var token = regContent.GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var orgData = new
        {
            name = "Test Org",
            description = "A test organization",
            registrationNumber = "123",
            taxNumber = "456"
        };

        var orgResp = await client.PostAsJsonAsync("/api/organisations", orgData);
        orgResp.EnsureSuccessStatusCode();
        var orgContent = await orgResp.Content.ReadFromJsonAsync<JsonElement>();
        orgContent.GetProperty("name").GetString().Should().Be("Test Org");

        var orgId = orgContent.GetProperty("id").GetGuid();
        var fetchResp = await client.GetAsync($"/api/organisations/{orgId}");
        fetchResp.EnsureSuccessStatusCode();
        var fetchContent = await fetchResp.Content.ReadFromJsonAsync<JsonElement>();
        fetchContent.GetProperty("name").GetString().Should().Be("Test Org");
    }
}
