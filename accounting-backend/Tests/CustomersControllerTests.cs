using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using AccountingApp.Tests.Helpers;
using Xunit;

namespace AccountingApp.Tests;

public class CustomersControllerTests : IClassFixture<AccountingWebApplicationFactory>
{
    private readonly AccountingWebApplicationFactory _factory;

    public CustomersControllerTests(AccountingWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient client, Guid orgId)> SetupAsync()
    {
        var client = _factory.CreateClient();
        var orgId = await AuthHelper.SetupWithOrganisationAsync(client);
        return (client, orgId);
    }

    private object ValidCustomerRequest(string name = "Acme Corp") => new
    {
        name,
        email = "customer@example.com",
        creditLimit = 5000m
    };

    // ==================== Positive Tests ====================

    [Fact]
    public async Task CreateCustomer_WithValidData_Returns201()
    {
        var (client, orgId) = await SetupAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/customers",
            ValidCustomerRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("name").GetString().Should().Be("Acme Corp");
        content.GetProperty("creditLimit").GetDecimal().Should().Be(5000m);
        content.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetCustomer_WithValidId_Returns200()
    {
        var (client, orgId) = await SetupAsync();

        var createResponse = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/customers",
            ValidCustomerRequest("Widget Co"));
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var customerId = created.GetProperty("id").GetGuid();

        var getResponse = await client.GetAsync($"/api/organisations/{orgId}/customers/{customerId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("id").GetGuid().Should().Be(customerId);
        content.GetProperty("name").GetString().Should().Be("Widget Co");
    }

    [Fact]
    public async Task GetCustomers_ReturnsListForOrganisation()
    {
        var (client, orgId) = await SetupAsync();

        await client.PostAsJsonAsync($"/api/organisations/{orgId}/customers", ValidCustomerRequest("Customer A"));
        await client.PostAsJsonAsync($"/api/organisations/{orgId}/customers", ValidCustomerRequest("Customer B"));

        var response = await client.GetAsync($"/api/organisations/{orgId}/customers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.ValueKind.Should().Be(JsonValueKind.Array);
        content.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task UpdateCustomer_WithValidData_Returns200()
    {
        var (client, orgId) = await SetupAsync();

        var createResponse = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/customers",
            ValidCustomerRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var customerId = created.GetProperty("id").GetGuid();

        var updateData = new { name = "Updated Corp", creditLimit = 10000m, isActive = true };
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/organisations/{orgId}/customers/{customerId}", updateData);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("name").GetString().Should().Be("Updated Corp");
        content.GetProperty("creditLimit").GetDecimal().Should().Be(10000m);
    }

    [Fact]
    public async Task DeleteCustomer_WithValidId_Returns204()
    {
        var (client, orgId) = await SetupAsync();

        var createResponse = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/customers",
            ValidCustomerRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var customerId = created.GetProperty("id").GetGuid();

        var deleteResponse = await client.DeleteAsync($"/api/organisations/{orgId}/customers/{customerId}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify soft-deleted (not returned in list)
        var listResponse = await client.GetAsync($"/api/organisations/{orgId}/customers");
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        list.GetArrayLength().Should().Be(0);
    }

    // ==================== Negative Tests ====================

    [Fact]
    public async Task CreateCustomer_WithoutAuthentication_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/organisations/{Guid.NewGuid()}/customers",
            ValidCustomerRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateCustomer_WithMissingName_Returns400()
    {
        var (client, orgId) = await SetupAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/customers",
            new { creditLimit = 1000m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateCustomer_WithInvalidEmail_Returns400()
    {
        var (client, orgId) = await SetupAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/customers",
            new { name = "Test Corp", email = "not-an-email", creditLimit = 1000m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateCustomer_WithNegativeCreditLimit_Returns400()
    {
        var (client, orgId) = await SetupAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/customers",
            new { name = "Test Corp", creditLimit = -100m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCustomer_WithNonExistentId_Returns404()
    {
        var (client, orgId) = await SetupAsync();

        var response = await client.GetAsync($"/api/organisations/{orgId}/customers/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("error").GetProperty("code").GetString().Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task UpdateCustomer_WithNonExistentId_Returns404()
    {
        var (client, orgId) = await SetupAsync();

        var updateData = new { name = "Updated", creditLimit = 1000m, isActive = true };
        var response = await client.PutAsJsonAsync(
            $"/api/organisations/{orgId}/customers/{Guid.NewGuid()}", updateData);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteCustomer_WithNonExistentId_Returns404()
    {
        var (client, orgId) = await SetupAsync();

        var response = await client.DeleteAsync($"/api/organisations/{orgId}/customers/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
