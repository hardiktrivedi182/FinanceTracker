using System.Text;
using FinanceTracker.Application.Accounts.Persistence;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.ImportBatches.Dtos;
using FinanceTracker.Application.ImportBatches.Persistence;
using FinanceTracker.Application.ImportBatches.Services;
using FinanceTracker.Application.Statements.Dtos;
using FinanceTracker.Application.Statements.Parsing;
using FinanceTracker.Application.Statements.Persistence;
using FinanceTracker.Application.Statements.Storage;
using FinanceTracker.Application.Transactions.Persistence;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace FinanceTracker.Application.Tests.ImportBatches;

public class ImportBatchServiceTests
{
    private readonly IImportBatchRepository _batchRepo = Substitute.For<IImportBatchRepository>();
    private readonly IStatementRepository _statementRepo = Substitute.For<IStatementRepository>();
    private readonly IAccountRepository _accountRepo = Substitute.For<IAccountRepository>();
    private readonly ITransactionRepository _transactionRepo = Substitute.For<ITransactionRepository>();
    private readonly IStatementStorage _statementStorage = Substitute.For<IStatementStorage>();
    private readonly ICsvStatementParser _csvParser = Substitute.For<ICsvStatementParser>();
    private readonly ImportFingerprintService _fingerprintService = new();
    private readonly IHouseholdContext _householdContext = Substitute.For<IHouseholdContext>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();

    private readonly Guid _householdId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _accountId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _statementId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly string _userId = "user-123";
    private readonly DateTimeOffset _now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly Account _activeAccount;
    private readonly Statement _parsedStatement;

    public ImportBatchServiceTests()
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(_userId);

        _householdContext.HasHousehold.Returns(true);
        _householdContext.HouseholdId.Returns(_householdId);

        _timeProvider.GetUtcNow().Returns(_now);

        _activeAccount = new Account(
            _accountId,
            _householdId,
            "Household Checking",
            AccountType.Checking,
            OwnershipType.Joint,
            null,
            "USD",
            _userId,
            _now);

        _parsedStatement = new Statement(
            _statementId,
            _householdId,
            _userId,
            "bank_statement.csv",
            StatementFormat.Csv,
            1024,
            "storage-key-1",
            _now);
        _parsedStatement.MarkParsed(2);

        _accountRepo.GetByIdAsync(_householdId, _accountId, Arg.Any<CancellationToken>())
            .Returns(_activeAccount);

        _statementRepo.GetByIdAsync(_statementId, Arg.Any<CancellationToken>())
            .Returns(_parsedStatement);

        _statementStorage.OpenReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes("Date,Description,Amount\n2026-10-01,Coffee,-4.50\n2026-10-02,Salary,3000")));
    }

    private ImportBatchService CreateService() =>
        new(
            _batchRepo,
            _statementRepo,
            _accountRepo,
            _transactionRepo,
            _statementStorage,
            _csvParser,
            _fingerprintService,
            _householdContext,
            _currentUser,
            _timeProvider);

    [Fact]
    public async Task CreateBatchAsync_WhenUnauthenticated_ThrowsUnauthorizedAccessException()
    {
        _currentUser.IsAuthenticated.Returns(false);
        var service = CreateService();

        var act = async () => await service.CreateBatchAsync(_statementId, _accountId);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task CreateBatchAsync_WhenAccountBelongsToAnotherHousehold_ThrowsUnauthorizedAccessException()
    {
        var otherHouseholdAccount = new Account(
            Guid.NewGuid(),
            Guid.NewGuid(), // different household
            "Other Account",
            AccountType.Checking,
            OwnershipType.Joint,
            null,
            "USD",
            "other-user",
            _now);

        _accountRepo.GetByIdAsync(_householdId, otherHouseholdAccount.Id, Arg.Any<CancellationToken>())
            .Returns(otherHouseholdAccount);

        var service = CreateService();
        var act = async () => await service.CreateBatchAsync(_statementId, otherHouseholdAccount.Id);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task CreateBatchAsync_WhenAccountIsInactive_ThrowsInvalidOperationException()
    {
        _activeAccount.Deactivate(_now);
        var service = CreateService();

        var act = async () => await service.CreateBatchAsync(_statementId, _accountId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*deactivated account*");
    }

    [Fact]
    public async Task CreateBatchAsync_WithValidRowsAndDuplicate_FlagsDuplicateCorrectly()
    {
        var rows = new List<ParsedTransactionRowDto>
        {
            new(2, new DateOnly(2026, 10, 1), "Coffee Shop", 4.50m, "Debit", null),
            new(3, new DateOnly(2026, 10, 2), "Salary Deposit", 3000.00m, "Credit", null),
            new(4, new DateOnly(2026, 10, 1), "Coffee Shop", 4.50m, "Debit", null) // duplicate within batch
        };

        _csvParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new CsvParseResult(true, 3, rows, Array.Empty<StatementParseErrorDto>(), "Generic CSV", null));

        ImportBatch? capturedBatch = null;
        await _batchRepo.AddAsync(Arg.Do<ImportBatch>(b => capturedBatch = b), Arg.Any<CancellationToken>());
        _batchRepo.GetByIdWithRowsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => capturedBatch);

        var service = CreateService();
        var result = await service.CreateBatchAsync(_statementId, _accountId);

        result.Should().NotBeNull();
        result.Batch.TotalRowCount.Should().Be(3);
        result.Batch.DuplicateCount.Should().Be(1);
        result.Rows.Should().HaveCount(3);

        // First coffee row: Valid, not duplicate, selected
        result.Rows[0].DuplicateStatus.Should().Be(DuplicateStatus.None);
        result.Rows[0].IsSelected.Should().BeTrue();

        // Salary row: Valid, not duplicate, selected
        result.Rows[1].DuplicateStatus.Should().Be(DuplicateStatus.None);
        result.Rows[1].IsSelected.Should().BeTrue();

        // Second coffee row: duplicate within batch, unselected
        result.Rows[2].DuplicateStatus.Should().Be(DuplicateStatus.PossibleDuplicate);
        result.Rows[2].DuplicateReason.Should().Contain("earlier in this import batch");
        result.Rows[2].IsSelected.Should().BeFalse();
    }

    [Fact]
    public async Task CreateBatchAsync_WithExistingTransactionInAccount_FlagsDuplicate()
    {
        var existingTxFp = _fingerprintService.ComputeFingerprint(
            _accountId,
            new DateOnly(2026, 10, 1),
            "Coffee Shop",
            4.50m,
            TransactionType.Debit,
            "USD");

        var existingTx = new Transaction(
            Guid.NewGuid(),
            _householdId,
            _accountId,
            null,
            null,
            1,
            new DateOnly(2026, 10, 1),
            null,
            "Coffee Shop",
            4.50m,
            TransactionType.Debit,
            "USD",
            existingTxFp,
            _userId,
            _now);

        _transactionRepo.GetByAccountIdAsync(_accountId, Arg.Any<CancellationToken>())
            .Returns(new List<Transaction> { existingTx });

        var rows = new List<ParsedTransactionRowDto>
        {
            new(2, new DateOnly(2026, 10, 1), "Coffee Shop", 4.50m, "Debit", null)
        };

        _csvParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new CsvParseResult(true, 1, rows, Array.Empty<StatementParseErrorDto>(), "Generic CSV", null));

        ImportBatch? capturedBatch = null;
        await _batchRepo.AddAsync(Arg.Do<ImportBatch>(b => capturedBatch = b), Arg.Any<CancellationToken>());
        _batchRepo.GetByIdWithRowsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => capturedBatch);

        var service = CreateService();
        var result = await service.CreateBatchAsync(_statementId, _accountId);

        result.Rows[0].DuplicateStatus.Should().Be(DuplicateStatus.PossibleDuplicate);
        result.Rows[0].DuplicateReason.Should().Contain("matches existing transaction");
        result.Rows[0].IsSelected.Should().BeFalse();
    }

    [Fact]
    public async Task CommitBatchAsync_WhenUnresolvedDuplicateExists_ThrowsInvalidOperationException()
    {
        var batch = new ImportBatch(Guid.NewGuid(), _householdId, _statementId, _accountId, _userId, _now);
        var row = new ImportStagingRow(
            Guid.NewGuid(),
            _householdId,
            batch.Id,
            2,
            null,
            new DateOnly(2026, 10, 1),
            "Coffee",
            4.50m,
            TransactionType.Debit,
            "USD",
            "fp-1",
            StagingRowValidationStatus.Valid,
            null,
            DuplicateStatus.PossibleDuplicate,
            "Possible duplicate");

        // Force selection without resolving
        row.SetSelection(true);
        batch.AddRow(row);

        _batchRepo.GetByIdWithRowsAsync(batch.Id, Arg.Any<CancellationToken>())
            .Returns(batch);

        var service = CreateService();
        var act = async () => await service.CommitBatchAsync(batch.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*duplicate and has not been explicitly reviewed and approved*");
    }

    [Fact]
    public async Task CommitBatchAsync_WhenApprovedAsIs_SucceedsAndCreatesTransaction()
    {
        var batch = new ImportBatch(Guid.NewGuid(), _householdId, _statementId, _accountId, _userId, _now);
        var row = new ImportStagingRow(
            Guid.NewGuid(),
            _householdId,
            batch.Id,
            2,
            null,
            new DateOnly(2026, 10, 1),
            "Coffee",
            4.50m,
            TransactionType.Debit,
            "USD",
            "fp-1",
            StagingRowValidationStatus.Valid,
            null,
            DuplicateStatus.PossibleDuplicate,
            "Possible duplicate");

        row.ResolveApprovedAsIs("Approved as legitimate second coffee");
        batch.AddRow(row);

        _batchRepo.GetByIdWithRowsAsync(batch.Id, Arg.Any<CancellationToken>())
            .Returns(batch);

        IReadOnlyList<Transaction>? savedTransactions = null;
        await _batchRepo.CommitBatchAtomicAsync(
            batch,
            Arg.Do<IReadOnlyList<Transaction>>(txs => savedTransactions = txs),
            Arg.Any<Statement>(),
            Arg.Any<CancellationToken>());

        var service = CreateService();
        var result = await service.CommitBatchAsync(batch.Id);

        result.Should().NotBeNull();
        result.ImportedCount.Should().Be(1);
        result.Status.Should().Be(ImportBatchStatus.Committed);

        savedTransactions.Should().NotBeNull();
        savedTransactions!.Should().HaveCount(1);
        savedTransactions![0].Description.Should().Be("Coffee");
        savedTransactions![0].Amount.Should().Be(4.50m);
        savedTransactions![0].SignedAmount.Should().Be(-4.50m);
        savedTransactions![0].Type.Should().Be(TransactionType.Debit);
        savedTransactions![0].AccountId.Should().Be(_accountId);
        savedTransactions![0].ImportBatchId.Should().Be(batch.Id);
        savedTransactions![0].StatementId.Should().Be(_statementId);
    }

    [Fact]
    public async Task CommitBatchAsync_WhenAlreadyCommitted_ReturnsIdempotentResultWithoutRecreatingTransactions()
    {
        var batch = new ImportBatch(Guid.NewGuid(), _householdId, _statementId, _accountId, _userId, _now);
        batch.MarkCommitted(_userId, _now.AddMinutes(-5), 2);

        _batchRepo.GetByIdWithRowsAsync(batch.Id, Arg.Any<CancellationToken>())
            .Returns(batch);

        var service = CreateService();
        var result = await service.CommitBatchAsync(batch.Id);

        result.Should().NotBeNull();
        result.Status.Should().Be(ImportBatchStatus.Committed);
        result.ImportedCount.Should().Be(2);
        result.Message.Should().Contain("already committed");

        // Verify CommitBatchAtomicAsync was never called again
        await _batchRepo.DidNotReceiveWithAnyArgs()
            .CommitBatchAtomicAsync(default!, default!, default!, default);
    }
}
