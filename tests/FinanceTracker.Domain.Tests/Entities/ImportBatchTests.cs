using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FinanceTracker.Domain.Tests.Entities;

public class ImportBatchTests
{
    private readonly Guid _batchId = Guid.NewGuid();
    private readonly Guid _householdId = Guid.NewGuid();
    private readonly Guid _statementId = Guid.NewGuid();
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    [Fact]
    public void Batch_WithValidRowsOnly_TransitionsToReadyToCommit()
    {
        var batch = new ImportBatch(_batchId, _householdId, _statementId, _accountId, "user-1", _now);
        batch.Status.Should().Be(ImportBatchStatus.AwaitingReview);

        var row = new ImportStagingRow(
            Guid.NewGuid(),
            _householdId,
            _batchId,
            2,
            null,
            new DateOnly(2026, 10, 1),
            "Salary",
            3000m,
            TransactionType.Credit,
            "USD",
            "fp-1",
            StagingRowValidationStatus.Valid,
            null);

        batch.AddRow(row);

        batch.TotalRowCount.Should().Be(1);
        batch.ReadyCount.Should().Be(1);
        batch.ErrorCount.Should().Be(0);
        batch.DuplicateCount.Should().Be(0);
        batch.Status.Should().Be(ImportBatchStatus.ReadyToCommit);
    }

    [Fact]
    public void Batch_WithUnresolvedIssues_TransitionsToReviewRequired()
    {
        var batch = new ImportBatch(_batchId, _householdId, _statementId, _accountId, "user-1", _now);

        var validRow = new ImportStagingRow(
            Guid.NewGuid(),
            _householdId,
            _batchId,
            2,
            null,
            new DateOnly(2026, 10, 1),
            "Groceries",
            50m,
            TransactionType.Debit,
            "USD",
            "fp-1",
            StagingRowValidationStatus.Valid,
            null);

        var invalidRow = new ImportStagingRow(
            Guid.NewGuid(),
            _householdId,
            _batchId,
            3,
            null,
            null,
            "",
            0m,
            null,
            "USD",
            "",
            StagingRowValidationStatus.Invalid,
            "Malformed amount");

        batch.AddRow(validRow);
        batch.AddRow(invalidRow);

        batch.TotalRowCount.Should().Be(2);
        batch.ErrorCount.Should().Be(1);
        batch.Status.Should().Be(ImportBatchStatus.ReviewRequired);

        // Exclude the invalid row
        invalidRow.ResolveExcluded("Malformed source row discarded");
        batch.RecalculateCountsAndStatus();

        // Now that the only remaining selected candidate is valid, batch becomes committable
        batch.ReadyCount.Should().Be(1);
        batch.Status.Should().Be(ImportBatchStatus.ReadyToCommit);
    }

    [Fact]
    public void MarkCommitted_SetsCommittedStatusAndMetadata_AndBecomesImmutable()
    {
        var batch = new ImportBatch(_batchId, _householdId, _statementId, _accountId, "user-1", _now);
        batch.MarkCommitted("user-partner-1", _now.AddMinutes(5), 15);

        batch.Status.Should().Be(ImportBatchStatus.Committed);
        batch.CommittedByUserId.Should().Be("user-partner-1");
        batch.CommittedCount.Should().Be(15);
        batch.CommittedAt.Should().Be(_now.AddMinutes(5));

        // Re-committing throws InvalidOperationException
        var actRecommit = () => batch.MarkCommitted("user-partner-1", _now.AddMinutes(6), 15);
        actRecommit.Should().Throw<InvalidOperationException>()
            .WithMessage("*already been committed*");

        // Cancelling committed batch throws InvalidOperationException
        var actCancel = () => batch.MarkCancelled();
        actCancel.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot cancel an already committed*");
    }
}
