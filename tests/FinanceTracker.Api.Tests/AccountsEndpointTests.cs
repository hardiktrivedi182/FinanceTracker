using System.Net;
using System.Net.Http.Json;
using FinanceTracker.Application.Accounts.Dtos;
using FinanceTracker.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinanceTracker.Api.Tests;

public class AccountsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AccountsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAccounts_WithoutAuthentication_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Request with no auth headers
        var response = await client.GetAsync("/api/accounts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateAccount_WithAuthenticatedPartner_ReturnsCreatedAndPersistsAccount()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "partner-1");

        var request = new CreateAccountRequest(
            "Joint Emergency Fund",
            AccountType.Savings,
            OwnershipType.Joint,
            null,
            "USD",
            InitialBalance: 15000m);

        var response = await client.PostAsJsonAsync("/api/accounts", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<AccountDto>();
        created.Should().NotBeNull();
        created!.Name.Should().Be("Joint Emergency Fund");
        created.AccountType.Should().Be(AccountType.Savings);
        created.OwnershipType.Should().Be(OwnershipType.Joint);
        created.LatestBalance.Should().Be(15000m);
        created.CreatedByUserId.Should().Be("partner-1");
    }

    [Fact]
    public async Task CreateIndianRetirementAsset_WithIndividualOwnership_Succeeds()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "partner-1");

        var request = new CreateAccountRequest(
            "Partner 1 EPF",
            AccountType.Epf,
            OwnershipType.Individual,
            "partner-1",
            "INR",
            InitialBalance: 850000m);

        var response = await client.PostAsJsonAsync("/api/accounts", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<AccountDto>();
        created.Should().NotBeNull();
        created!.AccountType.Should().Be(AccountType.Epf);
        created.OwnershipType.Should().Be(OwnershipType.Individual);
        created.OwnerUserId.Should().Be("partner-1");
        created.CurrencyCode.Should().Be("INR");
    }

    [Fact]
    public async Task UpdateAccount_WhenPartner2AttemptsToModifyPartner1IndividualAccount_ReturnsForbidden()
    {
        var clientPartner1 = _factory.CreateClient();
        clientPartner1.DefaultRequestHeaders.Add("X-User-Id", "partner-1");
        clientPartner1.DefaultRequestHeaders.Add("X-Household-Role", "Member");

        // Partner 1 creates an individual account
        var createRequest = new CreateAccountRequest(
            "Partner 1 Private ISA",
            AccountType.Investment,
            OwnershipType.Individual,
            "partner-1",
            "USD");

        var createResponse = await clientPartner1.PostAsJsonAsync("/api/accounts", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<AccountDto>();

        // Partner 2 attempts to rename Partner 1's individual account
        var clientPartner2 = _factory.CreateClient();
        clientPartner2.DefaultRequestHeaders.Add("X-User-Id", "partner-2");
        clientPartner2.DefaultRequestHeaders.Add("X-Household-Role", "Member");

        var updateRequest = new UpdateAccountRequest(
            "Hacked Name",
            AccountType.Investment,
            OwnershipType.Individual,
            "partner-2");

        var updateResponse = await clientPartner2.PutAsJsonAsync($"/api/accounts/{created!.Id}", updateRequest);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RecordValuation_UpdatesLatestValuationOnAccount()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "partner-1");

        var createResponse = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest(
            "PPF Scheme 2026",
            AccountType.Ppf,
            OwnershipType.Individual,
            "partner-1",
            "INR"));

        var created = await createResponse.Content.ReadFromJsonAsync<AccountDto>();

        var valuationRequest = new RecordValuationRequest(
            new DateOnly(2026, 3, 31),
            250000m,
            "Financial year end valuation",
            "Passbook-2026");

        var valResponse = await client.PostAsJsonAsync($"/api/accounts/{created!.Id}/valuations", valuationRequest);
        var content = await valResponse.Content.ReadAsStringAsync();
        valResponse.StatusCode.Should().Be(HttpStatusCode.Created, because: content);

        var getResponse = await client.GetAsync($"/api/accounts/{created.Id}");
        var fetched = await getResponse.Content.ReadFromJsonAsync<AccountDto>();

        fetched!.LatestBalance.Should().Be(250000m);
        fetched.LatestValuationDate.Should().Be(new DateOnly(2026, 3, 31));
    }

    [Fact]
    public async Task GetAccountById_WhenAccountBelongsToDifferentHousehold_ReturnsNotFound()
    {
        // Seed an account into an entirely different household
        var otherHouseholdId = Guid.NewGuid();
        var otherAccountId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinanceTracker.Infrastructure.Persistence.FinanceTrackerDbContext>();
            var otherHousehold = new FinanceTracker.Domain.Entities.Household(otherHouseholdId, "Foreign Household", now);
            var otherAccount = new FinanceTracker.Domain.Entities.Account(
                otherAccountId,
                otherHouseholdId,
                "Foreign Private Account",
                AccountType.Savings,
                OwnershipType.Joint,
                null,
                "USD",
                "foreign-user",
                now);

            db.Households.Add(otherHousehold);
            db.Accounts.Add(otherAccount);
            await db.SaveChangesAsync();
        }

        // Authenticated client belongs to DefaultHouseholdId (Household 1)
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "partner-1");

        // Attempt to fetch foreign account
        var response = await client.GetAsync($"/api/accounts/{otherAccountId}");

        // Must return 404 because cross-household queries are strictly partitioned
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

