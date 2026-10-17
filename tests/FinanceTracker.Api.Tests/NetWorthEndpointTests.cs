using System.Net;
using System.Net.Http.Json;
using FinanceTracker.Application.Accounts.Dtos;
using FinanceTracker.Application.NetWorth.Dtos;
using FinanceTracker.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FinanceTracker.Api.Tests;

public class NetWorthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public NetWorthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetNetWorth_WithoutAuthentication_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/api/household/net-worth");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetNetWorth_WithAuthenticatedUser_ReturnsCalculatedReport()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "partner-1");

        // Create an account with valuation
        var account = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest(
            "Mutual Fund Portfolio",
            AccountType.Investment,
            OwnershipType.Joint,
            null,
            "INR",
            InitialBalance: 750000m,
            InitialValuationDate: new DateOnly(2026, 4, 1)));
        account.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.GetAsync("/api/household/net-worth?asOf=2026-06-01");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var report = await response.Content.ReadFromJsonAsync<HouseholdNetWorthReportDto>();
        report.Should().NotBeNull();
        report!.Currencies.Should().Contain(c => c.CurrencyCode == "INR");

        var inr = report.Currencies.First(c => c.CurrencyCode == "INR");
        inr.TotalAssets.Should().BeGreaterThanOrEqualTo(750000m);
    }

    [Fact]
    public async Task UpdateValuation_WhenPartner2AttemptsToEditPartner1IndividualValuation_ReturnsForbidden()
    {
        // Partner 1 creates an individual account
        var clientP1 = _factory.CreateClient();
        clientP1.DefaultRequestHeaders.Add("X-User-Id", "partner-1");

        var createAcc = await clientP1.PostAsJsonAsync("/api/accounts", new CreateAccountRequest(
            "Partner 1 Secret PPF",
            AccountType.Ppf,
            OwnershipType.Individual,
            "partner-1",
            "INR",
            InitialBalance: 150000m,
            InitialValuationDate: new DateOnly(2026, 3, 31)));
        createAcc.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createAcc.Content.ReadFromJsonAsync<AccountDto>();

        // Get valuation ID
        var valResponse = await clientP1.GetAsync($"/api/accounts/{created!.Id}/valuations");
        var valuations = await valResponse.Content.ReadFromJsonAsync<List<AccountValuationDto>>();
        valuations.Should().NotBeNullOrEmpty();
        var valId = valuations![0].Id;

        // Partner 2 (Member) attempts to modify Partner 1's individual valuation
        var clientP2 = _factory.CreateClient();
        clientP2.DefaultRequestHeaders.Add("X-User-Id", "partner-2");

        var updateRequest = new UpdateValuationRequest(
            new DateOnly(2026, 3, 31),
            999999m,
            ValuationSourceType.Manual,
            "Unauthorized modification");

        var updateResponse = await clientP2.PutAsJsonAsync(
            $"/api/accounts/{created.Id}/valuations/{valId}",
            updateRequest);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateValuation_WhenOwnerEditsValuation_ReturnsOkAndUpdatesAuditFields()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "partner-1");

        var createAcc = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest(
            "Joint Fixed Deposit",
            AccountType.FixedDeposit,
            OwnershipType.Joint,
            null,
            "INR",
            InitialBalance: 500000m,
            InitialValuationDate: new DateOnly(2026, 4, 1)));
        createAcc.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createAcc.Content.ReadFromJsonAsync<AccountDto>();

        var valResponse = await client.GetAsync($"/api/accounts/{created!.Id}/valuations");
        var valuations = await valResponse.Content.ReadFromJsonAsync<List<AccountValuationDto>>();
        var valId = valuations![0].Id;

        var updateRequest = new UpdateValuationRequest(
            new DateOnly(2026, 4, 2),
            520000m,
            ValuationSourceType.Reconciled,
            "Bank interest credited",
            "FD-Ref-101");

        var updateResult = await client.PutAsJsonAsync(
            $"/api/accounts/{created.Id}/valuations/{valId}",
            updateRequest);

        updateResult.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedDto = await updateResult.Content.ReadFromJsonAsync<AccountValuationDto>();

        updatedDto.Should().NotBeNull();
        updatedDto!.Amount.Should().Be(520000m);
        updatedDto.ValuationDate.Should().Be(new DateOnly(2026, 4, 2));
        updatedDto.SourceType.Should().Be(ValuationSourceType.Reconciled);
        updatedDto.UpdatedByUserId.Should().Be("partner-1");
        updatedDto.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CompareNetWorth_WhenDate1AfterDate2_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "partner-1");

        var response = await client.GetAsync("/api/household/net-worth/compare?date1=2026-06-01&date2=2026-01-01");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
