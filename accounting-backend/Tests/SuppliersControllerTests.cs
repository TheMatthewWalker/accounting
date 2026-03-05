using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using AccountingApp.Tests.Helpers;
using Xunit;

namespace AccountingApp.Tests;

public class SuppliersControllerTests : IClassFixture<AccountingWebApplicationFactory>
{
    private readonly AccountingWebApplicationFactory _factory;

    public SuppliersControllerTests(AccountingWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient client, Guid orgId)> SetupAsync()
    {
        var client = _factory.CreateClient();
        var orgId = await AuthHelper.SetupWithOrganisationAsync(client);
        return (client, orgId);
    }

    private object ValidSupplierRequest(string name = "Supplier Inc") => new
    {
        name,
        email = "supplier@example.com",
        creditLimit = 10000m
    };

    // ==================== Positive Tests ====================

    [Fact]
    public async Task CreateSupplier_WithValidData_Returns201()
    {
        var (client, orgId) = await SetupAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/suppliers",
            ValidSupplierRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("name").GetString().Should().Be("Supplier Inc");
        content.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetSupplier_WithValidId_Returns200()
    {
        var (client, orgId) = await SetupAsync();

        var createResponse = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/suppliers",
            ValidSupplierRequest("Parts Ltd"));
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var supplierId = created.GetProperty("id").GetGuid();

        var getResponse = await client.GetAsync($"/api/organisations/{orgId}/suppliers/{supplierId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("id").GetGuid().Should().Be(supplierId);
        content.GetProperty("name").GetString().Should().Be("Parts Ltd");
    }

    [Fact]
    public async Task GetSuppliers_ReturnsListForOrganisation()
    {
        var (client, orgId) = await SetupAsync();

        await client.PostAsJsonAsync($"/api/organisations/{orgId}/suppliers", ValidSupplierRequest("Supplier A"));
        await client.PostAsJsonAsync($"/api/organisations/{orgId}/suppliers", ValidSupplierRequest("Supplier B"));

        var response = await client.GetAsync($"/api/organisations/{orgId}/suppliers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.ValueKind.Should().Be(JsonValueKind.Array);
        content.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task UpdateSupplier_WithValidData_Returns200()
    {
        var (client, orgId) = await SetupAsync();

        var createResponse = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/suppliers",
            ValidSupplierRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var supplierId = created.GetProperty("id").GetGuid();

        var updateData = new { name = "Updated Supplier", creditLimit = 20000m, isActive = true };
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/organisations/{orgId}/suppliers/{supplierId}", updateData);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("name").GetString().Should().Be("Updated Supplier");
    }

    [Fact]
    public async Task DeleteSupplier_WithValidId_Returns204()
    {
        var (client, orgId) = await SetupAsync();

        var createResponse = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/suppliers",
            ValidSupplierRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var supplierId = created.GetProperty("id").GetGuid();

        var deleteResponse = await client.DeleteAsync($"/api/organisations/{orgId}/suppliers/{supplierId}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ==================== Negative Tests ====================

    [Fact]
    public async Task CreateSupplier_WithoutAuthentication_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/organisations/{Guid.NewGuid()}/suppliers",
            ValidSupplierRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateSupplier_WithMissingName_Returns400()
    {
        var (client, orgId) = await SetupAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/suppliers",
            new { creditLimit = 1000m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSupplier_WithInvalidEmail_Returns400()
    {
        var (client, orgId) = await SetupAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/suppliers",
            new { name = "Test Supplier", email = "not-an-email", creditLimit = 1000m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSupplier_WithNegativeCreditLimit_Returns400()
    {
        var (client, orgId) = await SetupAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/suppliers",
            new { name = "Test Supplier", creditLimit = -500m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetSupplier_WithNonExistentId_Returns404()
    {
        var (client, orgId) = await SetupAsync();

        var response = await client.GetAsync($"/api/organisations/{orgId}/suppliers/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("error").GetProperty("code").GetString().Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task UpdateSupplier_WithNonExistentId_Returns404()
    {
        var (client, orgId) = await SetupAsync();

        var updateData = new { name = "Updated", creditLimit = 1000m, isActive = true };
        var response = await client.PutAsJsonAsync(
            $"/api/organisations/{orgId}/suppliers/{Guid.NewGuid()}", updateData);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteSupplier_WithNonExistentId_Returns404()
    {
        var (client, orgId) = await SetupAsync();

        var response = await client.DeleteAsync($"/api/organisations/{orgId}/suppliers/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
