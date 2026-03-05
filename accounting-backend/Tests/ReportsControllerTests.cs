using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using AccountingApp.Tests.Helpers;
using Xunit;

namespace AccountingApp.Tests;

public class ReportsControllerTests : IClassFixture<AccountingWebApplicationFactory>
{
    private readonly AccountingWebApplicationFactory _factory;

    public ReportsControllerTests(AccountingWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient client, Guid orgId, Guid debitAccountId, Guid creditAccountId)> SetupWithPostedEntryAsync()
    {
        var client = _factory.CreateClient();
        var orgId = await AuthHelper.SetupWithOrganisationAsync(client);

        // Create two GL accounts
        var debitResponse = await client.PostAsJsonAsync($"/api/organisations/{orgId}/glaccounts",
            new { code = "1000", name = "Cash", type = "Asset", openingBalance = 0m });
        var debitAccount = await debitResponse.Content.ReadFromJsonAsync<JsonElement>();
        var debitId = debitAccount.GetProperty("id").GetGuid();

        var creditResponse = await client.PostAsJsonAsync($"/api/organisations/{orgId}/glaccounts",
            new { code = "4000", name = "Revenue", type = "Revenue", openingBalance = 0m });
        var creditAccount = await creditResponse.Content.ReadFromJsonAsync<JsonElement>();
        var creditId = creditAccount.GetProperty("id").GetGuid();

        // Create and post a journal entry
        var entryData = new
        {
            type = "Sales",
            entryDate = DateTime.UtcNow.Date.ToString("o"),
            description = "Test sale",
            lines = new[]
            {
                new { glAccountId = debitId, debitAmount = 500m, creditAmount = 0m },
                new { glAccountId = creditId, debitAmount = 0m, creditAmount = 500m }
            }
        };

        var entryResponse = await client.PostAsJsonAsync($"/api/organisations/{orgId}/daybook", entryData);
        var entry = await entryResponse.Content.ReadFromJsonAsync<JsonElement>();
        var entryId = entry.GetProperty("id").GetGuid();

        await client.PostAsJsonAsync($"/api/organisations/{orgId}/daybook/{entryId}/post", new { });

        return (client, orgId, debitId, creditId);
    }

    // ==================== Trial Balance Tests ====================

    [Fact]
    public async Task GetTrialBalance_WithPostedEntries_Returns200WithBalances()
    {
        var (client, orgId, _, _) = await SetupWithPostedEntryAsync();
        var asOfDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");

        var response = await client.GetAsync($"/api/organisations/{orgId}/reports/trial-balance?asOfDate={asOfDate}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("totalDebits").GetDecimal().Should().Be(content.GetProperty("totalCredits").GetDecimal());
        content.GetProperty("lines").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetTrialBalance_WithNoEntries_Returns200WithEmptyLines()
    {
        var client = _factory.CreateClient();
        var orgId = await AuthHelper.SetupWithOrganisationAsync(client);
        var asOfDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");

        var response = await client.GetAsync($"/api/organisations/{orgId}/reports/trial-balance?asOfDate={asOfDate}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("lines").GetArrayLength().Should().Be(0);
        content.GetProperty("totalDebits").GetDecimal().Should().Be(0m);
        content.GetProperty("totalCredits").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task GetTrialBalance_OnlyIncludesPostedEntries()
    {
        var client = _factory.CreateClient();
        var orgId = await AuthHelper.SetupWithOrganisationAsync(client);

        // Create GL accounts
        var debitResp = await client.PostAsJsonAsync($"/api/organisations/{orgId}/glaccounts",
            new { code = "1000", name = "Cash", type = "Asset", openingBalance = 0m });
        var debitId = (await debitResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var creditResp = await client.PostAsJsonAsync($"/api/organisations/{orgId}/glaccounts",
            new { code = "4000", name = "Revenue", type = "Revenue", openingBalance = 0m });
        var creditId = (await creditResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Create entry but do NOT post it
        await client.PostAsJsonAsync($"/api/organisations/{orgId}/daybook", new
        {
            type = "Sales",
            entryDate = DateTime.UtcNow.Date.ToString("o"),
            lines = new[]
            {
                new { glAccountId = debitId, debitAmount = 500m, creditAmount = 0m },
                new { glAccountId = creditId, debitAmount = 0m, creditAmount = 500m }
            }
        });

        var asOfDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
        var response = await client.GetAsync($"/api/organisations/{orgId}/reports/trial-balance?asOfDate={asOfDate}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Unposted entry should NOT appear in trial balance
        content.GetProperty("lines").GetArrayLength().Should().Be(0);
    }

    // ==================== T-Account Tests ====================

    [Fact]
    public async Task GetTAccounts_WithPostedEntries_Returns200()
    {
        var (client, orgId, _, _) = await SetupWithPostedEntryAsync();
        var asOfDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");

        var response = await client.GetAsync($"/api/organisations/{orgId}/reports/taccounts?asOfDate={asOfDate}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.ValueKind.Should().Be(JsonValueKind.Array);
        content.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetTAccount_ForSpecificAccount_Returns200WithCorrectBalance()
    {
        var (client, orgId, debitId, _) = await SetupWithPostedEntryAsync();
        var asOfDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");

        var response = await client.GetAsync($"/api/organisations/{orgId}/reports/taccounts/{debitId}?asOfDate={asOfDate}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("accountId").GetGuid().Should().Be(debitId);
        content.GetProperty("closingBalance").GetDecimal().Should().Be(500m); // 500 debit
    }

    [Fact]
    public async Task GetTAccount_ForNonExistentAccount_Returns404()
    {
        var (client, orgId, _, _) = await SetupWithPostedEntryAsync();
        var asOfDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");

        var response = await client.GetAsync($"/api/organisations/{orgId}/reports/taccounts/{Guid.NewGuid()}?asOfDate={asOfDate}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ==================== General Ledger Tests ====================

    [Fact]
    public async Task GetGeneralLedger_WithPostedEntries_Returns200()
    {
        var (client, orgId, _, _) = await SetupWithPostedEntryAsync();
        var fromDate = DateTime.UtcNow.Date.AddDays(-1).ToString("yyyy-MM-dd");
        var toDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");

        var response = await client.GetAsync($"/api/organisations/{orgId}/reports/general-ledger?fromDate={fromDate}&toDate={toDate}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("entries").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetGeneralLedger_WithInvalidDateRange_Returns400()
    {
        var client = _factory.CreateClient();
        var orgId = await AuthHelper.SetupWithOrganisationAsync(client);

        var fromDate = DateTime.UtcNow.Date.AddDays(10).ToString("yyyy-MM-dd");
        var toDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");

        var response = await client.GetAsync($"/api/organisations/{orgId}/reports/general-ledger?fromDate={fromDate}&toDate={toDate}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetTrialBalance_WithoutAuthentication_Returns401()
    {
        var client = _factory.CreateClient();
        var asOfDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");

        var response = await client.GetAsync($"/api/organisations/{Guid.NewGuid()}/reports/trial-balance?asOfDate={asOfDate}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
