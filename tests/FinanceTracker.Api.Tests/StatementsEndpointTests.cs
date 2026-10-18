using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FinanceTracker.Application.Statements.Dtos;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinanceTracker.Api.Tests;

public class StatementsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public StatementsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Upload_WithoutAuthentication_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("Date,Description,Amount\n2026-01-01,Coffee,-4.50")), "file", "test.csv");

        var response = await client.PostAsync("/api/statements/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Upload_WithCookieAuthentication_WithoutXsrfToken_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "dev_user_id=partner-1");

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("Date,Description,Amount\n2026-01-01,Coffee,-4.50")), "file", "test.csv");

        var response = await client.PostAsync("/api/statements/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Antiforgery validation failed");
    }

    [Fact]
    public async Task Upload_WithCookieAuthentication_WithValidXsrfToken_Succeeds()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "dev_user_id=partner-1");

        // Step 1: Request CSRF token from the API
        var csrfResponse = await client.GetAsync("/api/auth/csrf-token");
        csrfResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var csrfJson = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = csrfJson.GetProperty("token").GetString();
        token.Should().NotBeNullOrWhiteSpace();

        // Extract the Set-Cookie headers for the antiforgery cookie and dev session
        var cookies = csrfResponse.Headers.GetValues("Set-Cookie").ToList();
        var antiforgeryCookie = cookies.FirstOrDefault(c => c.StartsWith(".AspNetCore.Antiforgery", StringComparison.Ordinal));

        // Step 2: Perform upload sending cookie + X-XSRF-Token header
        var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/statements/upload");
        uploadRequest.Headers.Add("X-XSRF-Token", token);

        var cookieHeader = "dev_user_id=partner-1";
        if (antiforgeryCookie != null)
        {
            var cookieVal = antiforgeryCookie.Split(';')[0];
            cookieHeader += "; " + cookieVal;
        }
        uploadRequest.Headers.Add("Cookie", cookieHeader);

        var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("Date,Description,Amount\n2026-01-01,Coffee,-4.50")), "file", "coffee.csv");
        uploadRequest.Content = multipart;

        var uploadResponse = await client.SendAsync(uploadRequest);

        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await uploadResponse.Content.ReadFromJsonAsync<StatementUploadResultDto>();
        result.Should().NotBeNull();
        result!.Status.Should().Be(StatementProcessingStatus.Parsed);
        result.ParsedTransactionCount.Should().Be(1);
        result.SampleRows.Should().HaveCount(1);
        result.Message.Should().Contain("0 transactions imported");
    }

    [Fact]
    public async Task Upload_WithDevHeader_WithoutCookies_Succeeds()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "partner-1");

        using var content = new MultipartFormDataContent();
        var csv = """
            Date,Description,Debit,Credit,Balance
            2026-02-01,Groceries,65.20,,1500.00
            2026-02-02,Interest,,12.50,1512.50
            """;
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "statement.csv");

        var response = await client.PostAsync("/api/statements/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<StatementUploadResultDto>();
        result.Should().NotBeNull();
        result!.Status.Should().Be(StatementProcessingStatus.Parsed);
        result.ParsedTransactionCount.Should().Be(2);
        result.SampleRows.Should().HaveCount(2);

        // Verify storage key is never exposed
        var rawJson = await response.Content.ReadAsStringAsync();
        rawJson.Should().NotContain("StorageKey");
        rawJson.Should().NotContain("storage_key");
        rawJson.Should().NotContain(".blob.core.windows.net");
    }

    [Fact]
    public async Task Upload_DisguisedExe_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "partner-1");

        using var content = new MultipartFormDataContent();
        byte[] mzExe = [0x4D, 0x5A, 0x90, 0x00];
        content.Add(new ByteArrayContent(mzExe), "file", "disguised.csv");

        var response = await client.PostAsync("/api/statements/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("executable");
    }

    [Fact]
    public async Task Upload_EmptyFile_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "partner-1");

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([]), "file", "empty.csv");

        var response = await client.PostAsync("/api/statements/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_OversizedFile_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "partner-1");

        // 10 MB + 1 byte
        var largeBuffer = new byte[10 * 1024 * 1024 + 1];
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(largeBuffer), "file", "huge.csv");

        var response = await client.PostAsync("/api/statements/upload", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CrossHousehold_Statements_CannotBeAccessedByAnotherHousehold()
    {
        var foreignHouseholdId = Guid.NewGuid();
        var foreignStatementId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinanceTrackerDbContext>();
            var foreignHousehold = new Household(foreignHouseholdId, "Foreign Family", now);
            var foreignStatement = new Statement(
                foreignStatementId,
                foreignHouseholdId,
                "foreign-user",
                "foreign.csv",
                StatementFormat.Csv,
                500,
                "foreign-key",
                now);

            db.Households.Add(foreignHousehold);
            db.Statements.Add(foreignStatement);
            await db.SaveChangesAsync();
        }

        // Authenticated client belongs to DefaultHouseholdId
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "partner-1");

        // Attempt to fetch foreign statement
        var response = await client.GetAsync($"/api/statements/{foreignStatementId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Upload_NeverMutatesLedgerOrAccountValuations()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "partner-1");

        // 1. Snapshot accounts and valuations before statement upload
        var accountsBefore = await client.GetFromJsonAsync<List<FinanceTracker.Application.Accounts.Dtos.AccountDto>>("/api/accounts");
        accountsBefore.Should().NotBeNull();

        // 2. Upload statement
        using var content = new MultipartFormDataContent();
        var csv = """
            Date,Description,Amount
            2026-03-01,Groceries,-120.00
            2026-03-02,Salary,3000.00
            """;
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "isolated_test.csv");

        var response = await client.PostAsync("/api/statements/upload", content);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var uploadResult = await response.Content.ReadFromJsonAsync<StatementUploadResultDto>();
        uploadResult.Should().NotBeNull();
        uploadResult!.Status.Should().Be(StatementProcessingStatus.Parsed);
        uploadResult.ParsedTransactionCount.Should().Be(2);

        // 3. Snapshot accounts after upload - must be identical, ledger is untouched
        var accountsAfter = await client.GetFromJsonAsync<List<FinanceTracker.Application.Accounts.Dtos.AccountDto>>("/api/accounts");
        accountsAfter.Should().NotBeNull();
        accountsAfter.Should().BeEquivalentTo(accountsBefore, "uploading and parsing must never create, alter or mutate accounts or balances");

        // 4. Verify statement was tracked in statements history
        var statements = await client.GetFromJsonAsync<List<StatementSummaryDto>>("/api/statements");
        statements.Should().NotBeNull();
        statements.Should().Contain(s => s.OriginalFileName == "isolated_test.csv" && s.Status == StatementProcessingStatus.Parsed);
    }
}
