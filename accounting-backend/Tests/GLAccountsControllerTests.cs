using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using AccountingApp.Tests.Helpers;
using Xunit;

namespace AccountingApp.Tests;

public class GLAccountsControllerTests : IClassFixture<AccountingWebApplicationFactory>
{
    private readonly AccountingWebApplicationFactory _factory;

    public GLAccountsControllerTests(AccountingWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient client, Guid orgId)> SetupAsync()
    {
        var client = _factory.CreateClient();
        var orgId = await AuthHelper.SetupWithOrganisationAsync(client);
        return (client, orgId);
    }

    private object ValidAccountRequest(string code = "1000", string type = "Asset") => new
    {
        code,
        name = $"Account {code}",
        type,
        openingBalance = 0m
    };

    // ==================== Positive Tests ====================

    [Fact]
    public async Task CreateAccount_WithValidData_Returns201()
    {
        var (client, orgId) = await SetupAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/glaccounts",
            ValidAccountRequest("1001", "Asset"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("code").GetString().Should().Be("1001");
        content.GetProperty("type").GetString().Should().Be("Asset");
        content.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData("Asset")]
    [InlineData("Liability")]
    [InlineData("Equity")]
    [InlineData("Revenue")]
    [InlineData("Expense")]
    public async Task CreateAccount_AllValidTypes_Returns201(string accountType)
    {
        var (client, orgId) = await SetupAsync();
        var code = accountType.Substring(0, 3).ToUpper() + "01";

        var response = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/glaccounts",
            ValidAccountRequest(code, accountType));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetAccount_WithValidId_Returns200()
    {
        var (client, orgId) = await SetupAsync();

        var createResponse = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/glaccounts",
            ValidAccountRequest("2000"));
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accountId = created.GetProperty("id").GetGuid();

        var getResponse = await client.GetAsync($"/api/organisations/{orgId}/glaccounts/{accountId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("id").GetGuid().Should().Be(accountId);
        content.GetProperty("code").GetString().Should().Be("2000");
    }

    [Fact]
    public async Task GetAccounts_ReturnsListForOrganisation()
    {
        var (client, orgId) = await SetupAsync();

        await client.PostAsJsonAsync($"/api/organisations/{orgId}/glaccounts", ValidAccountRequest("3001"));
        await client.PostAsJsonAsync($"/api/organisations/{orgId}/glaccounts", ValidAccountRequest("3002"));

        var response = await client.GetAsync($"/api/organisations/{orgId}/glaccounts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.ValueKind.Should().Be(JsonValueKind.Array);
        content.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task UpdateAccount_WithValidData_Returns200()
    {
        var (client, orgId) = await SetupAsync();

        var createResponse = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/glaccounts",
            ValidAccountRequest("4000"));
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accountId = created.GetProperty("id").GetGuid();

        var updateData = new { name = "Updated Account Name", isActive = true };
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/organisations/{orgId}/glaccounts/{accountId}", updateData);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("name").GetString().Should().Be("Updated Account Name");
    }

    [Fact]
    public async Task DeleteAccount_WithValidId_Returns204()
    {
        var (client, orgId) = await SetupAsync();

        var createResponse = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/glaccounts",
            ValidAccountRequest("5000"));
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accountId = created.GetProperty("id").GetGuid();

        var deleteResponse = await client.DeleteAsync($"/api/organisations/{orgId}/glaccounts/{accountId}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ==================== Negative Tests ====================

    [Fact]
    public async Task CreateAccount_WithoutAuthentication_Returns401()
    {
        var client = _factory.CreateClient();
        var orgId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/glaccounts",
            ValidAccountRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateAccount_WithInvalidType_Returns400()
    {
        var (client, orgId) = await SetupAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/glaccounts",
            new { code = "9001", name = "Test", type = "InvalidType", openingBalance = 0m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAccount_WithMissingCode_Returns400()
    {
        var (client, orgId) = await SetupAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/glaccounts",
            new { name = "Test Account", type = "Asset", openingBalance = 0m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAccount_WithDuplicateCode_Returns409()
    {
        var (client, orgId) = await SetupAsync();

        await client.PostAsJsonAsync($"/api/organisations/{orgId}/glaccounts", ValidAccountRequest("6001"));

        var response = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/glaccounts",
            ValidAccountRequest("6001"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("error").GetProperty("code").GetString().Should().Be("DUPLICATE_RESOURCE");
    }

    [Fact]
    public async Task GetAccount_WithNonExistentId_Returns404()
    {
        var (client, orgId) = await SetupAsync();

        var response = await client.GetAsync($"/api/organisations/{orgId}/glaccounts/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("error").GetProperty("code").GetString().Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task UpdateAccount_WithNonExistentId_Returns404()
    {
        var (client, orgId) = await SetupAsync();

        var updateData = new { name = "Updated", isActive = true };
        var response = await client.PutAsJsonAsync(
            $"/api/organisations/{orgId}/glaccounts/{Guid.NewGuid()}", updateData);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAccount_WithNonExistentId_Returns404()
    {
        var (client, orgId) = await SetupAsync();

        var response = await client.DeleteAsync($"/api/organisations/{orgId}/glaccounts/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
