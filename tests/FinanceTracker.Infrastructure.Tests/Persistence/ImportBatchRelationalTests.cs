using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceTracker.Infrastructure.Tests.Persistence;

/// <summary>
/// Relational integration tests executing against SQL Server LocalDB.
/// Validates transaction atomicity, commit rollback, optimistic concurrency via SQL Server rowversion,
/// and duplicate fingerprint index behavior.
/// </summary>
public class ImportBatchRelationalTests : IAsyncLifetime
{
    private readonly string _dbName = $"FinanceTracker_RelTest_{Guid.NewGuid():N}";
    private string ConnectionString => $"Server=(localdb)\\mssqllocaldb;Database={_dbName};Integrated Security=true;TrustServerCertificate=True;";
    private DbContextOptions<FinanceTrackerDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        _options = new DbContextOptionsBuilder<FinanceTrackerDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        await using var context = new FinanceTrackerDbContext(_options);
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        try
        {
            await using var context = new FinanceTrackerDbContext(_options);
            await context.Database.EnsureDeletedAsync();
        }
        catch
        {
            // Best effort cleanup
        }
    }

    private FinanceTrackerDbContext CreateContext() => new(_options);

    [Fact]
    public async Task CommitBatchAtomicAsync_Succeeds_PersistsLedgerAndMarksBatchCommitted()
    {
        var householdId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var statementId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var userId = "user-rel-1";
        var now = DateTimeOffset.UtcNow;

        await using (var seedCtx = CreateContext())
        {
            var household = new Household(householdId, "Relational Household", now);
            var account = new Account(accountId, householdId, "Checking Account", AccountType.Checking, OwnershipType.Joint, null, "USD", userId, now);
            var statement = new Statement(statementId, householdId, userId, "october.csv", StatementFormat.Csv, 1024, "key-1", now);
            statement.MarkParsed(1);

            var batch = new ImportBatch(batchId, householdId, statementId, accountId, userId, now);
            var row = new ImportStagingRow(
                Guid.NewGuid(),
                householdId,
                batchId,
                1,
                "2026-10-01,Coffee,4.50",
                new DateOnly(2026, 10, 1),
                "Coffee",
                4.50m,
                TransactionType.Debit,
                "USD",
                "fp-coffee-1",
                StagingRowValidationStatus.Valid,
                null);

            batch.AddRow(row);

            seedCtx.Households.Add(household);
            seedCtx.Accounts.Add(account);
            seedCtx.Statements.Add(statement);
            seedCtx.ImportBatches.Add(batch);
            await seedCtx.SaveChangesAsync();
        }

        // Commit batch using repository
        await using (var commitCtx = CreateContext())
        {
            var repo = new ImportBatchRepository(commitCtx);
            var batch = await repo.GetByIdWithRowsAsync(batchId);
            batch.Should().NotBeNull();

            var txId = Guid.NewGuid();
            var tx = new Transaction(
                txId,
                householdId,
                accountId,
                statementId,
                batchId,
                1,
                new DateOnly(2026, 10, 1),
                null,
                "Coffee",
                4.50m,
                TransactionType.Debit,
                "USD",
                "fp-coffee-1",
                userId,
                now);

            batch!.MarkCommitted(userId, now, 1);
            batch.Statement.MarkCommitted();
            batch.Rows.First().MarkCommitted(txId);

            await repo.CommitBatchAtomicAsync(batch, new[] { tx }, batch.Statement);
        }

        // Verify in fresh context
        await using (var verifyCtx = CreateContext())
        {
            var savedBatch = await verifyCtx.ImportBatches
                .Include(b => b.Rows)
                .Include(b => b.Statement)
                .FirstOrDefaultAsync(b => b.Id == batchId);

            savedBatch.Should().NotBeNull();
            savedBatch!.Status.Should().Be(ImportBatchStatus.Committed);
            savedBatch.CommittedCount.Should().Be(1);
            savedBatch.RowVersion.Should().NotBeNull();
            savedBatch.Statement.Status.Should().Be(StatementProcessingStatus.Committed);

            var savedTx = await verifyCtx.Transactions.FirstOrDefaultAsync(t => t.ImportBatchId == batchId);
            savedTx.Should().NotBeNull();
            savedTx!.Description.Should().Be("Coffee");
            savedTx.Amount.Should().Be(4.5000m);
            savedTx.SignedAmount.Should().Be(-4.5000m);
            savedTx.CurrencyCode.Should().Be("USD");
            savedTx.AccountId.Should().Be(accountId);
            savedTx.HouseholdId.Should().Be(householdId);
        }
    }

    [Fact]
    public async Task CommitBatchAtomicAsync_RollbackOnError_LeavesDatabaseUntouched()
    {
        var householdId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var statementId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var userId = "user-rel-rollback";
        var now = DateTimeOffset.UtcNow;

        await using (var seedCtx = CreateContext())
        {
            var household = new Household(householdId, "Rollback Household", now);
            var account = new Account(accountId, householdId, "Checking Account", AccountType.Checking, OwnershipType.Joint, null, "USD", userId, now);
            var statement = new Statement(statementId, householdId, userId, "rollback.csv", StatementFormat.Csv, 1024, "key-rb", now);
            statement.MarkParsed(1);

            var batch = new ImportBatch(batchId, householdId, statementId, accountId, userId, now);
            var row = new ImportStagingRow(
                Guid.NewGuid(),
                householdId,
                batchId,
                1,
                null,
                new DateOnly(2026, 10, 1),
                "Book",
                15m,
                TransactionType.Debit,
                "USD",
                "fp-book",
                StagingRowValidationStatus.Valid,
                null);

            batch.AddRow(row);

            seedCtx.Households.Add(household);
            seedCtx.Accounts.Add(account);
            seedCtx.Statements.Add(statement);
            seedCtx.ImportBatches.Add(batch);
            await seedCtx.SaveChangesAsync();
        }

        // Attempt commit with a transaction that causes a DB constraint violation (non-existent AccountId FK)
        await using (var commitCtx = CreateContext())
        {
            var repo = new ImportBatchRepository(commitCtx);
            var batch = await repo.GetByIdWithRowsAsync(batchId);
            batch.Should().NotBeNull();

            var nonExistentAccountId = Guid.NewGuid();
            var invalidTx = new Transaction(
                Guid.NewGuid(),
                householdId,
                nonExistentAccountId, // Invalid FK to trigger SQL Server FK constraint failure
                statementId,
                batchId,
                1,
                new DateOnly(2026, 10, 1),
                null,
                "Invalid Tx",
                15m,
                TransactionType.Debit,
                "USD",
                "fp-book",
                userId,
                now);

            batch!.MarkCommitted(userId, now, 1);

            var act = async () => await repo.CommitBatchAtomicAsync(batch, new[] { invalidTx }, batch.Statement);
            await act.Should().ThrowAsync<Exception>();
        }

        // Verify that neither the batch status nor any transaction was persisted
        await using (var verifyCtx = CreateContext())
        {
            var batchAfter = await verifyCtx.ImportBatches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == batchId);
            batchAfter.Should().NotBeNull();
            batchAfter!.Status.Should().NotBe(ImportBatchStatus.Committed);

            var txCount = await verifyCtx.Transactions.CountAsync(t => t.ImportBatchId == batchId);
            txCount.Should().Be(0);
        }
    }

    [Fact]
    public async Task CommitBatchAtomicAsync_ConcurrentCommit_DetectsOptimisticConcurrency()
    {
        var householdId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var statementId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var userId = "user-rel-concur";
        var now = DateTimeOffset.UtcNow;

        await using (var seedCtx = CreateContext())
        {
            var household = new Household(householdId, "Concur Household", now);
            var account = new Account(accountId, householdId, "Checking", AccountType.Checking, OwnershipType.Joint, null, "USD", userId, now);
            var statement = new Statement(statementId, householdId, userId, "concur.csv", StatementFormat.Csv, 1024, "key-c", now);
            statement.MarkParsed(1);

            var batch = new ImportBatch(batchId, householdId, statementId, accountId, userId, now);
            var row = new ImportStagingRow(
                Guid.NewGuid(),
                householdId,
                batchId,
                1,
                null,
                new DateOnly(2026, 10, 1),
                "Lunch",
                12.50m,
                TransactionType.Debit,
                "USD",
                "fp-lunch",
                StagingRowValidationStatus.Valid,
                null);

            batch.AddRow(row);

            seedCtx.Households.Add(household);
            seedCtx.Accounts.Add(account);
            seedCtx.Statements.Add(statement);
            seedCtx.ImportBatches.Add(batch);
            await seedCtx.SaveChangesAsync();
        }

        // Context 1 and Context 2 both load the batch concurrently (sharing the initial rowversion)
        await using var ctx1 = CreateContext();
        await using var ctx2 = CreateContext();

        var repo1 = new ImportBatchRepository(ctx1);
        var repo2 = new ImportBatchRepository(ctx2);

        var batch1 = await repo1.GetByIdWithRowsAsync(batchId);
        var batch2 = await repo2.GetByIdWithRowsAsync(batchId);

        batch1.Should().NotBeNull();
        batch2.Should().NotBeNull();

        // Prepare transactions for both commits
        var tx1 = new Transaction(
            Guid.NewGuid(), householdId, accountId, statementId, batchId, 1,
            new DateOnly(2026, 10, 1), null, "Lunch", 12.50m, TransactionType.Debit, "USD", "fp-lunch", userId, now);

        var tx2 = new Transaction(
            Guid.NewGuid(), householdId, accountId, statementId, batchId, 1,
            new DateOnly(2026, 10, 1), null, "Lunch", 12.50m, TransactionType.Debit, "USD", "fp-lunch", userId, now);

        batch1!.MarkCommitted(userId, now, 1);
        batch2!.MarkCommitted(userId, now, 1);

        // Commit 1 executes first and succeeds, incrementing the SQL Server rowversion
        await repo1.CommitBatchAtomicAsync(batch1, new[] { tx1 }, batch1.Statement);

        // Commit 2 attempts to commit using the stale rowversion from batch2
        // Concurrency detection catches the conflict, notices batch is now Committed, and behaves idempotently
        await repo2.CommitBatchAtomicAsync(batch2, new[] { tx2 }, batch2.Statement);

        // Verify that only 1 transaction exists in the database
        await using (var verifyCtx = CreateContext())
        {
            var txList = await verifyCtx.Transactions.Where(t => t.ImportBatchId == batchId).ToListAsync();
            txList.Should().HaveCount(1);
            txList[0].Id.Should().Be(tx1.Id);
        }
    }

    [Fact]
    public async Task UpdateAsync_RacingWithCommit_DetectsOptimisticConcurrency()
    {
        var householdId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var statementId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var userId = "user-rel-race";
        var now = DateTimeOffset.UtcNow;

        await using (var seedCtx = CreateContext())
        {
            var household = new Household(householdId, "Race Household", now);
            var account = new Account(accountId, householdId, "Checking", AccountType.Checking, OwnershipType.Joint, null, "USD", userId, now);
            var statement = new Statement(statementId, householdId, userId, "race.csv", StatementFormat.Csv, 1024, "key-r", now);
            statement.MarkParsed(1);

            var batch = new ImportBatch(batchId, householdId, statementId, accountId, userId, now);
            var row = new ImportStagingRow(
                Guid.NewGuid(),
                householdId,
                batchId,
                1,
                null,
                new DateOnly(2026, 10, 1),
                "Hardware Store",
                45m,
                TransactionType.Debit,
                "USD",
                "fp-hw",
                StagingRowValidationStatus.Valid,
                null);

            batch.AddRow(row);

            seedCtx.Households.Add(household);
            seedCtx.Accounts.Add(account);
            seedCtx.Statements.Add(statement);
            seedCtx.ImportBatches.Add(batch);
            await seedCtx.SaveChangesAsync();
        }

        // Two contexts load batch
        await using var ctxCommit = CreateContext();
        await using var ctxEdit = CreateContext();

        var repoCommit = new ImportBatchRepository(ctxCommit);
        var repoEdit = new ImportBatchRepository(ctxEdit);

        var batchToCommit = await repoCommit.GetByIdWithRowsAsync(batchId);
        var batchToEdit = await repoEdit.GetByIdWithRowsAsync(batchId);

        // An edit races and updates the batch first
        batchToEdit!.Rows.First().ResolveExcluded("Excluded by user");
        batchToEdit.RecalculateCountsAndStatus();
        await repoEdit.UpdateAsync(batchToEdit);

        // Now commit attempts to execute with the stale batchToCommit (prior rowversion)
        var tx = new Transaction(
            Guid.NewGuid(), householdId, accountId, statementId, batchId, 1,
            new DateOnly(2026, 10, 1), null, "Hardware Store", 45m, TransactionType.Debit, "USD", "fp-hw", userId, now);

        batchToCommit!.MarkCommitted(userId, now, 1);

        var act = async () => await repoCommit.CommitBatchAtomicAsync(batchToCommit, new[] { tx }, batchToCommit.Statement);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*modified concurrently by another operation*");
    }

    [Fact]
    public async Task DuplicateProtection_NonUniqueAccountFingerprint_AllowsExplicitlyApprovedDuplicates()
    {
        var householdId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var userId = "user-rel-dup";
        var now = DateTimeOffset.UtcNow;

        await using (var seedCtx = CreateContext())
        {
            var household = new Household(householdId, "Dup Household", now);
            var account = new Account(accountId, householdId, "Checking", AccountType.Checking, OwnershipType.Joint, null, "USD", userId, now);

            seedCtx.Households.Add(household);
            seedCtx.Accounts.Add(account);
            await seedCtx.SaveChangesAsync();
        }

        // Insert two distinct transactions with identical (AccountId, Fingerprint) representing legitimate repeated purchases
        var sharedFingerprint = "9a8b7c6d5e4f3a2b1c0d";
        var tx1 = new Transaction(
            Guid.NewGuid(), householdId, accountId, null, null, 1,
            new DateOnly(2026, 10, 1), null, "Coffee", 4.50m, TransactionType.Debit, "USD", sharedFingerprint, userId, now);

        var tx2 = new Transaction(
            Guid.NewGuid(), householdId, accountId, null, null, 2,
            new DateOnly(2026, 10, 1), null, "Coffee", 4.50m, TransactionType.Debit, "USD", sharedFingerprint, userId, now.AddMinutes(1));

        await using (var testCtx = CreateContext())
        {
            testCtx.Transactions.AddRange(tx1, tx2);
            var saveAct = async () => await testCtx.SaveChangesAsync();

            // Must succeed without unique constraint violation on (account_id, fingerprint)
            await saveAct.Should().NotThrowAsync();
        }

        await using (var verifyCtx = CreateContext())
        {
            var persisted = await verifyCtx.Transactions
                .Where(t => t.AccountId == accountId && t.Fingerprint == sharedFingerprint)
                .ToListAsync();

            persisted.Should().HaveCount(2);
        }
    }
}
