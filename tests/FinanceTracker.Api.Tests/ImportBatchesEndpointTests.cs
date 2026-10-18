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

    [Fact]
    public async Task CreateBatch_TargetingOtherUserIndividualAccount_AsMember_ReturnsForbidden()
    {
        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add("X-User-Id", "partner-1");

        // 1. Partner 1 creates an individual account owned by partner-1
        var accountRequest = new CreateAccountRequest(
            "Partner 1 Private Account",
            AccountType.Checking,
            OwnershipType.Individual,
            "partner-1",
            "USD");

        var accountResponse = await adminClient.PostAsJsonAsync("/api/accounts", accountRequest);
        accountResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdAccount = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();
        var accountId = createdAccount!.Id;

        // 2. Upload a statement
        var csv = "Date,Description,Amount\n2026-10-01,Groceries,-25.00";
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "groceries.csv");
        var uploadResponse = await adminClient.PostAsync("/api/statements/upload", multipart);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<StatementUploadResultDto>();
        var statementId = uploadResult!.StatementId;

        // 3. Partner 2 (household member, not admin and not owner) attempts to create an import batch for Partner 1's individual account
        var memberClient = _factory.CreateClient();
        memberClient.DefaultRequestHeaders.Add("X-User-Id", "partner-2");

        var createBatchReq = new CreateImportBatchRequest(statementId, accountId);
        var response = await memberClient.PostAsJsonAsync("/api/import-batches", createBatchReq);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateStagingRow_SelectingInvalidRow_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "partner-1");

        // 1. Create a joint account
        var accountRequest = new CreateAccountRequest(
            "Validation Test Account",
            AccountType.Checking,
            OwnershipType.Joint,
            null,
            "USD");
        var accountResponse = await client.PostAsJsonAsync("/api/accounts", accountRequest);
        var createdAccount = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();

        // 2. Upload statement with unparseable/invalid row (e.g. zero amount)
        var csv = "Date,Description,Amount\n2026-10-01,Zero Amount Item,0.00";
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "invalid.csv");
        var uploadResponse = await client.PostAsync("/api/statements/upload", multipart);
        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<StatementUploadResultDto>();

        // 3. Create batch
        var createBatchReq = new CreateImportBatchRequest(uploadResult!.StatementId, createdAccount!.Id);
        var batchResponse = await client.PostAsJsonAsync("/api/import-batches", createBatchReq);
        batchResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var batchDetail = await batchResponse.Content.ReadFromJsonAsync<ImportBatchDetailDto>();
        batchDetail.Should().NotBeNull();

        var invalidRow = batchDetail!.Rows.First(r => r.ValidationStatus == StagingRowValidationStatus.Invalid);

        // 4. Attempt to force select invalid row without correcting it
        var selectReq = new UpdateStagingRowRequest(
            IsSelected: true,
            Resolution: null,
            ResolutionNotes: null,
            ResolvedDate: null,
            ResolvedDescription: null,
            ResolvedAmount: null,
            ResolvedType: null);

        var updateResponse = await client.PutAsJsonAsync($"/api/import-batches/{batchDetail.Batch.Id}/rows/{invalidRow.Id}", selectReq);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
