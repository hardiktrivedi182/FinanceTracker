using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FinanceTracker.Domain.Tests.Entities;

public class ImportStagingRowTests
{
    private readonly Guid _batchId = Guid.NewGuid();
    private readonly Guid _householdId = Guid.NewGuid();
    private readonly DateOnly _date = new(2026, 10, 15);

    [Fact]
    public void Constructor_ValidRowWithoutDuplicate_IsSelectedByDefault()
    {
        var row = new ImportStagingRow(
            Guid.NewGuid(),
            _householdId,
            _batchId,
            2,
            "raw-line",
            _date,
            "Bookstore",
            24.99m,
            TransactionType.Debit,
            "USD",
            "fingerprint-123",
            StagingRowValidationStatus.Valid,
            null);

        row.IsSelected.Should().BeTrue();
        row.IsEligibleForCommit.Should().BeTrue();
        row.EffectiveAmount.Should().Be(24.99m);
        row.EffectiveDescription.Should().Be("Bookstore");
        row.EffectiveDate.Should().Be(_date);
    }

    [Fact]
    public void Constructor_DuplicateRow_IsNotSelectedByDefault()
    {
        var row = new ImportStagingRow(
            Guid.NewGuid(),
            _householdId,
            _batchId,
            3,
            null,
            _date,
            "Coffee",
            4.50m,
            TransactionType.Debit,
            "USD",
            "fingerprint-coffee",
            StagingRowValidationStatus.Valid,
            null,
            DuplicateStatus.PossibleDuplicate,
            "Matches existing transaction");

        row.IsSelected.Should().BeFalse();
        row.IsEligibleForCommit.Should().BeFalse();

        // User explicitly approves duplicate as-is
        row.ResolveApprovedAsIs("Legitimate repeated purchase");
        row.Resolution.Should().Be(RowResolution.ApprovedAsIs);
        row.IsSelected.Should().BeTrue();
        row.IsEligibleForCommit.Should().BeTrue();
    }

    [Fact]
    public void ResolveApprovedAsIs_OnInvalidRow_ThrowsInvalidOperationException()
    {
        var row = new ImportStagingRow(
            Guid.NewGuid(),
            _householdId,
            _batchId,
            4,
            null,
            _date,
            "",
            0m,
            TransactionType.Debit,
            "USD",
            "fingerprint",
            StagingRowValidationStatus.Invalid,
            "Amount is zero and description is empty");

        var act = () => row.ResolveApprovedAsIs();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot approve an invalid row*");
    }

    [Fact]
    public void ResolveManuallyCorrected_UpdatesEffectiveValues_AndValidates()
    {
        var row = new ImportStagingRow(
            Guid.NewGuid(),
            _householdId,
            _batchId,
            5,
            null,
            null,
            "Atm Withdrawal",
            null,
            null,
            "USD",
            "",
            StagingRowValidationStatus.Invalid,
            "Missing date and amount");

        row.IsEligibleForCommit.Should().BeFalse();

        row.ResolveManuallyCorrected(
            new DateOnly(2026, 10, 16),
            "Atm Withdrawal Main Branch",
            100.00m,
            TransactionType.Debit,
            "Added missing date and amount from receipt");

        row.ValidationStatus.Should().Be(StagingRowValidationStatus.Valid);
        row.EffectiveDate.Should().Be(new DateOnly(2026, 10, 16));
        row.EffectiveAmount.Should().Be(100.00m);
        row.EffectiveDescription.Should().Be("Atm Withdrawal Main Branch");
        row.EffectiveType.Should().Be(TransactionType.Debit);
        row.IsSelected.Should().BeTrue();
        row.IsEligibleForCommit.Should().BeTrue();
    }

    [Fact]
    public void ResolveExcluded_MarksExcludedAndUnselects()
    {
        var row = new ImportStagingRow(
            Guid.NewGuid(),
            _householdId,
            _batchId,
            6,
            null,
            _date,
            "Unknown Debit",
            50m,
            TransactionType.Debit,
            "USD",
            "fingerprint",
            StagingRowValidationStatus.Valid,
            null);

        row.ResolveExcluded("Not relevant for household budget");
        row.Resolution.Should().Be(RowResolution.Excluded);
        row.IsSelected.Should().BeFalse();
        row.IsEligibleForCommit.Should().BeFalse();
    }

    [Fact]
    public void SetSelection_True_OnInvalidRow_ThrowsInvalidOperationException()
    {
        var row = new ImportStagingRow(
            Guid.NewGuid(),
            _householdId,
            _batchId,
            7,
            null,
            _date,
            "",
            null,
            null,
            "USD",
            "",
            StagingRowValidationStatus.Invalid,
            "Missing description and amount");

        var act = () => row.SetSelection(true);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot select an invalid row*");
    }
}
