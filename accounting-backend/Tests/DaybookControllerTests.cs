using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using AccountingApp.Tests.Helpers;
using Xunit;

namespace AccountingApp.Tests;

public class DaybookControllerTests : IClassFixture<AccountingWebApplicationFactory>
{
    private readonly AccountingWebApplicationFactory _factory;

    public DaybookControllerTests(AccountingWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient client, Guid orgId, Guid debitAccountId, Guid creditAccountId)> SetupWithAccountsAsync()
    {
        var client = _factory.CreateClient();
        var orgId = await AuthHelper.SetupWithOrganisationAsync(client);

        var debitResponse = await client.PostAsJsonAsync($"/api/organisations/{orgId}/glaccounts",
            new { code = "1000", name = "Cash", type = "Asset", openingBalance = 0m });
        var debitAccount = await debitResponse.Content.ReadFromJsonAsync<JsonElement>();
        var debitAccountId = debitAccount.GetProperty("id").GetGuid();

        var creditResponse = await client.PostAsJsonAsync($"/api/organisations/{orgId}/glaccounts",
            new { code = "4000", name = "Revenue", type = "Revenue", openingBalance = 0m });
        var creditAccount = await creditResponse.Content.ReadFromJsonAsync<JsonElement>();
        var creditAccountId = creditAccount.GetProperty("id").GetGuid();

        return (client, orgId, debitAccountId, creditAccountId);
    }

    private object BalancedEntryRequest(Guid debitAccountId, Guid creditAccountId, DateTime? date = null) => new
    {
        type = "Sales",
        entryDate = (date ?? DateTime.UtcNow.Date).ToString("o"),
        description = "Test entry",
        lines = new[]
        {
            new { glAccountId = debitAccountId, debitAmount = 100m, creditAmount = 0m },
            new { glAccountId = creditAccountId, debitAmount = 0m, creditAmount = 100m }
        }
    };

    // ==================== Positive Tests ====================

    [Fact]
    public async Task CreateEntry_WithValidBalancedLines_Returns201()
    {
        var (client, orgId, debitId, creditId) = await SetupWithAccountsAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/daybook",
            BalancedEntryRequest(debitId, creditId));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("type").GetString().Should().Be("Sales");
        content.GetProperty("isPosted").GetBoolean().Should().BeFalse();
        content.GetProperty("lines").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task GetEntry_WithValidId_Returns200()
    {
        var (client, orgId, debitId, creditId) = await SetupWithAccountsAsync();

        var createResponse = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/daybook",
            BalancedEntryRequest(debitId, creditId));
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var entryId = created.GetProperty("id").GetGuid();

        var getResponse = await client.GetAsync($"/api/organisations/{orgId}/daybook/{entryId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("id").GetGuid().Should().Be(entryId);
    }

    [Fact]
    public async Task GetEntries_ReturnsListForOrganisation()
    {
        var (client, orgId, debitId, creditId) = await SetupWithAccountsAsync();

        await client.PostAsJsonAsync($"/api/organisations/{orgId}/daybook", BalancedEntryRequest(debitId, creditId));
        await client.PostAsJsonAsync($"/api/organisations/{orgId}/daybook", BalancedEntryRequest(debitId, creditId));

        var response = await client.GetAsync($"/api/organisations/{orgId}/daybook");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.ValueKind.Should().Be(JsonValueKind.Array);
        content.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task PostEntry_WithBalancedEntry_Returns200()
    {
        var (client, orgId, debitId, creditId) = await SetupWithAccountsAsync();

        var createResponse = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/daybook",
            BalancedEntryRequest(debitId, creditId));
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var entryId = created.GetProperty("id").GetGuid();

        var postResponse = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/daybook/{entryId}/post", new { });

        postResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify it's now posted
        var getResponse = await client.GetAsync($"/api/organisations/{orgId}/daybook/{entryId}");
        var content = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("isPosted").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task DeleteEntry_UnpostedEntry_Returns204()
    {
        var (client, orgId, debitId, creditId) = await SetupWithAccountsAsync();

        var createResponse = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/daybook",
            BalancedEntryRequest(debitId, creditId));
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var entryId = created.GetProperty("id").GetGuid();

        var deleteResponse = await client.DeleteAsync($"/api/organisations/{orgId}/daybook/{entryId}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetEntries_WithDateFilter_ReturnsFilteredResults()
    {
        var (client, orgId, debitId, creditId) = await SetupWithAccountsAsync();

        var today = DateTime.UtcNow.Date;
        await client.PostAsJsonAsync($"/api/organisations/{orgId}/daybook", BalancedEntryRequest(debitId, creditId, today));

        var fromDate = today.AddDays(-1).ToString("yyyy-MM-dd");
        var toDate = today.ToString("yyyy-MM-dd");
        var response = await client.GetAsync($"/api/organisations/{orgId}/daybook?fromDate={fromDate}&toDate={toDate}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetArrayLength().Should().BeGreaterThan(0);
    }

    // ==================== Negative Tests ====================

    [Fact]
    public async Task CreateEntry_WithFutureDate_Returns400()
    {
        var (client, orgId, debitId, creditId) = await SetupWithAccountsAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/daybook",
            BalancedEntryRequest(debitId, creditId, DateTime.UtcNow.Date.AddDays(1)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("error").GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task CreateEntry_WithInvalidEntryType_Returns400()
    {
        var (client, orgId, debitId, creditId) = await SetupWithAccountsAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/daybook",
            new
            {
                type = "InvalidType",
                entryDate = DateTime.UtcNow.Date.ToString("o"),
                lines = new[]
                {
                    new { glAccountId = debitId, debitAmount = 100m, creditAmount = 0m },
                    new { glAccountId = creditId, debitAmount = 0m, creditAmount = 100m }
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateEntry_WithSingleLine_Returns400()
    {
        var (client, orgId, debitId, creditId) = await SetupWithAccountsAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/daybook",
            new
            {
                type = "Journal",
                entryDate = DateTime.UtcNow.Date.ToString("o"),
                lines = new[]
                {
                    new { glAccountId = debitId, debitAmount = 100m, creditAmount = 0m }
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateEntry_WithNonExistentGLAccount_Returns404()
    {
        var (client, orgId, debitId, creditId) = await SetupWithAccountsAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/daybook",
            new
            {
                type = "Journal",
                entryDate = DateTime.UtcNow.Date.ToString("o"),
                lines = new[]
                {
                    new { glAccountId = Guid.NewGuid(), debitAmount = 100m, creditAmount = 0m },
                    new { glAccountId = creditId, debitAmount = 0m, creditAmount = 100m }
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostEntry_AlreadyPosted_Returns400()
    {
        var (client, orgId, debitId, creditId) = await SetupWithAccountsAsync();

        var createResponse = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/daybook",
            BalancedEntryRequest(debitId, creditId));
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var entryId = created.GetProperty("id").GetGuid();

        await client.PostAsJsonAsync($"/api/organisations/{orgId}/daybook/{entryId}/post", new { });

        // Try to post again
        var secondPostResponse = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/daybook/{entryId}/post", new { });

        secondPostResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await secondPostResponse.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("error").GetProperty("code").GetString().Should().Be("BUSINESS_RULE_VIOLATION");
    }

    [Fact]
    public async Task DeleteEntry_PostedEntry_Returns400()
    {
        var (client, orgId, debitId, creditId) = await SetupWithAccountsAsync();

        var createResponse = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/daybook",
            BalancedEntryRequest(debitId, creditId));
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var entryId = created.GetProperty("id").GetGuid();

        await client.PostAsJsonAsync($"/api/organisations/{orgId}/daybook/{entryId}/post", new { });

        var deleteResponse = await client.DeleteAsync($"/api/organisations/{orgId}/daybook/{entryId}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await deleteResponse.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("error").GetProperty("code").GetString().Should().Be("OPERATION_FAILED");
    }

    [Fact]
    public async Task GetEntry_WithNonExistentId_Returns404()
    {
        var (client, orgId, _, _) = await SetupWithAccountsAsync();

        var response = await client.GetAsync($"/api/organisations/{orgId}/daybook/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetEntries_WithInvalidDateRange_Returns400()
    {
        var (client, orgId, _, _) = await SetupWithAccountsAsync();

        var fromDate = DateTime.UtcNow.Date.AddDays(10).ToString("yyyy-MM-dd");
        var toDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
        var response = await client.GetAsync($"/api/organisations/{orgId}/daybook?fromDate={fromDate}&toDate={toDate}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostEntry_WithoutAuthentication_Returns401()
    {
        var client = _factory.CreateClient();
        var orgId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync(
            $"/api/organisations/{orgId}/daybook",
            new { });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
