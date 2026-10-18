using System.Net;
using System.Net.Http.Json;
using System.Text;
using FinanceTracker.Application.Accounts.Dtos;
using FinanceTracker.Application.ImportBatches.Dtos;
using FinanceTracker.Application.Statements.Dtos;
using FinanceTracker.Application.Transactions.Dtos;
using FinanceTracker.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FinanceTracker.Api.Tests;

public class ImportBatchesEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ImportBatchesEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetBatches_WithoutAuthentication_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/import-batches");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EndToEnd_ImportBatch_Preview_Resolve_Commit_Lifecycle()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "partner-1");

        // 1. Create a checking account to import into
        var accountRequest = new CreateAccountRequest(
            "Import Test Account",
            AccountType.Checking,
            OwnershipType.Joint,
            null,
            "USD");

        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        accountResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdAccount = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        createdAccount.Should().NotBeNull();
        var accountId = createdAccount!.Id;

        // 2. Upload a statement with 2 rows: 1 normal, 1 repeated twice (making 3 rows total)
        var csv = """
            Date,Description,Amount
            2026-10-01,Supermarket,-54.20
            2026-10-02,Coffee Shop,-4.50
            2026-10-02,Coffee Shop,-4.50
            """;

        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "transactions.csv");

        var uploadResponse = await client.PostAsync("/api/statements/upload", multipart);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<StatementUploadResultDto>();
        uploadResult.Should().NotBeNull();
        var statementId = uploadResult!.StatementId;

        // 3. Create import batch for this statement and account
        var createBatchReq = new CreateImportBatchRequest(statementId, accountId);
        var createBatchResponse = await client.PostAsJsonAsync("/api/import-batches", createBatchReq);
        createBatchResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var batchDetail = await createBatchResponse.Content.ReadFromJsonAsync<ImportBatchDetailDto>();

        batchDetail.Should().NotBeNull();
        batchDetail!.Batch.TotalRowCount.Should().Be(3);
        batchDetail.Batch.DuplicateCount.Should().Be(1);
        batchDetail.Batch.Status.Should().Be(ImportBatchStatus.ReviewRequired);
        batchDetail.Rows.Should().HaveCount(3);

        var batchId = batchDetail.Batch.Id;
        var duplicateRow = batchDetail.Rows.First(r => r.DuplicateStatus != DuplicateStatus.None);
        duplicateRow.IsSelected.Should().BeFalse();

        // 4. Attempting to commit with unapproved duplicate should fail if forced selected
        var selectUnapprovedReq = new UpdateStagingRowRequest(
            IsSelected: true,
            Resolution: RowResolution.None,
            ResolutionNotes: null,
            ResolvedDate: null,
            ResolvedDescription: null,
            ResolvedAmount: null,
            ResolvedType: null);

        var updateResp = await client.PutAsJsonAsync($"/api/import-batches/{batchId}/rows/{duplicateRow.Id}", selectUnapprovedReq);
        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var commitAttemptResponse = await client.PostAsync($"/api/import-batches/{batchId}/commit", null);
        commitAttemptResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 5. Explicitly approve the duplicate row as legitimate
        var approveReq = new UpdateStagingRowRequest(
            IsSelected: true,
            Resolution: RowResolution.ApprovedAsIs,
            ResolutionNotes: "Second coffee confirmed on same day",
            ResolvedDate: null,
            ResolvedDescription: null,
            ResolvedAmount: null,
            ResolvedType: null);

        var approveResp = await client.PutAsJsonAsync($"/api/import-batches/{batchId}/rows/{duplicateRow.Id}", approveReq);
        approveResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedDetail = await approveResp.Content.ReadFromJsonAsync<ImportBatchDetailDto>();
        updatedDetail!.Batch.Status.Should().Be(ImportBatchStatus.ReadyToCommit);
        updatedDetail.Batch.ReadyCount.Should().Be(3);

        // 6. Explicitly commit the batch
        var commitResponse = await client.PostAsync($"/api/import-batches/{batchId}/commit", null);
        commitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var commitResult = await commitResponse.Content.ReadFromJsonAsync<CommitImportBatchResultDto>();

        commitResult.Should().NotBeNull();
        commitResult!.Status.Should().Be(ImportBatchStatus.Committed);
        commitResult.ImportedCount.Should().Be(3);
        commitResult.ExcludedCount.Should().Be(0);

        // 7. Verify Idempotency: repeated commit does NOT duplicate transactions
        var recommitResponse = await client.PostAsync($"/api/import-batches/{batchId}/commit", null);
        recommitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var recommitResult = await recommitResponse.Content.ReadFromJsonAsync<CommitImportBatchResultDto>();
        recommitResult!.ImportedCount.Should().Be(3);
        recommitResult.Message.Should().Contain("already committed");

        // 8. Verify ledger transactions were created in the database and are queryable
        var txResponse = await client.GetAsync($"/api/transactions?accountId={accountId}");
        txResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var transactions = await txResponse.Content.ReadFromJsonAsync<IReadOnlyList<TransactionDto>>();

        transactions.Should().NotBeNull();
        transactions!.Should().HaveCount(3);
        transactions.All(t => t.AccountId == accountId).Should().BeTrue();
        transactions.All(t => t.CurrencyCode == "USD").Should().BeTrue();
        transactions.All(t => t.SignedAmount < 0).Should().BeTrue(); // All were debits
    }
}
