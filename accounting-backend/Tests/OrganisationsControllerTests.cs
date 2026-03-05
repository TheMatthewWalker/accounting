using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using AccountingApp.Tests.Helpers;
using Xunit;

namespace AccountingApp.Tests;

public class OrganisationsControllerTests : IClassFixture<AccountingWebApplicationFactory>
{
    private readonly AccountingWebApplicationFactory _factory;

    public OrganisationsControllerTests(AccountingWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UpdateOrganisation_WithValidData_Returns200()
    {
        var client = _factory.CreateClient();
        var orgId = await AuthHelper.SetupWithOrganisationAsync(client, "Original Name");

        var updateData = new
        {
            name = "Updated Name",
            description = "Updated description",
            registrationNumber = "REG-001",
            taxNumber = "TAX-001"
        };

        var response = await client.PutAsJsonAsync($"/api/organisations/{orgId}", updateData);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("name").GetString().Should().Be("Updated Name");
        content.GetProperty("description").GetString().Should().Be("Updated description");
        content.GetProperty("registrationNumber").GetString().Should().Be("REG-001");
        content.GetProperty("taxNumber").GetString().Should().Be("TAX-001");
    }

    [Fact]
    public async Task UpdateOrganisation_WithEmptyName_Returns400()
    {
        var client = _factory.CreateClient();
        var orgId = await AuthHelper.SetupWithOrganisationAsync(client);

        var updateData = new
        {
            name = "",
            description = "Some description"
        };

        var response = await client.PutAsJsonAsync($"/api/organisations/{orgId}", updateData);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateOrganisation_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        // Create org while authenticated
        var orgId = await AuthHelper.SetupWithOrganisationAsync(client);

        // Remove auth header
        client.DefaultRequestHeaders.Authorization = null;

        var updateData = new
        {
            name = "Should Fail",
            description = "No auth"
        };

        var response = await client.PutAsJsonAsync($"/api/organisations/{orgId}", updateData);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateOrganisation_NonExistent_Returns404()
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.RegisterAndGetTokenAsync(client);
        AuthHelper.SetBearerToken(client, token);

        var updateData = new
        {
            name = "Does Not Exist",
            description = "No org"
        };

        var response = await client.PutAsJsonAsync($"/api/organisations/{Guid.NewGuid()}", updateData);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
