using System.Text;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.Statements.Dtos;
using FinanceTracker.Application.Statements.Parsing;
using FinanceTracker.Application.Statements.Persistence;
using FinanceTracker.Application.Statements.Services;
using FinanceTracker.Application.Statements.Storage;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace FinanceTracker.Application.Tests.Statements;

public class StatementServiceTests
{
    private readonly IStatementRepository _statementRepo = Substitute.For<IStatementRepository>();
    private readonly IStatementStorage _statementStorage = Substitute.For<IStatementStorage>();
    private readonly ICsvStatementParser _csvParser = Substitute.For<ICsvStatementParser>();
    private readonly IHouseholdContext _householdContext = Substitute.For<IHouseholdContext>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();

    private readonly Guid _householdId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly string _userId = "user-123";
    private readonly DateTimeOffset _fixedNow = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    public StatementServiceTests()
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(_userId);

        _householdContext.HasHousehold.Returns(true);
        _householdContext.HouseholdId.Returns(_householdId);

        _timeProvider.GetUtcNow().Returns(_fixedNow);
    }

    private StatementService CreateService() =>
        new(_statementRepo, _statementStorage, _csvParser, _householdContext, _currentUser, _timeProvider);

    [Fact]
    public async Task UploadAndParseAsync_WhenUnauthenticated_ThrowsUnauthorizedAccessException()
    {
        _currentUser.IsAuthenticated.Returns(false);
        var service = CreateService();

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Date,Description,Amount\n2026-01-01,Test,10"));
        var act = async () => await service.UploadAndParseAsync("stmt.csv", 100, stream);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task UploadAndParseAsync_WhenNoHousehold_ThrowsInvalidOperationException()
    {
        _householdContext.HasHousehold.Returns(false);
        var service = CreateService();

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Date,Description,Amount\n2026-01-01,Test,10"));
        var act = async () => await service.UploadAndParseAsync("stmt.csv", 100, stream);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not associated with an active household*");
    }

    [Fact]
    public async Task UploadAndParseAsync_WhenFileEmpty_ThrowsArgumentException()
    {
        var service = CreateService();
        using var stream = new MemoryStream();

        var act = async () => await service.UploadAndParseAsync("stmt.csv", 0, stream);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*empty*");
    }

    [Fact]
    public async Task UploadAndParseAsync_WhenFileOversized_ThrowsArgumentException()
    {
        var service = CreateService();
        using var stream = new MemoryStream([1, 2, 3]);

        long overLimit = 10 * 1024 * 1024 + 1;
        var act = async () => await service.UploadAndParseAsync("stmt.csv", overLimit, stream);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*10 MB maximum limit*");
    }

    [Fact]
    public async Task UploadAndParseAsync_WhenExtensionNotCsv_ThrowsArgumentException()
    {
        var service = CreateService();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("col1,col2"));

        var act = async () => await service.UploadAndParseAsync("statement.pdf", 100, stream);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Only CSV files are supported*");
    }

    [Fact]
    public async Task UploadAndParseAsync_WhenDisguisedExecutable_ThrowsArgumentException()
    {
        var service = CreateService();
        // MZ header
        byte[] mzExe = [0x4D, 0x5A, 0x90, 0x00, 0x03];
        using var stream = new MemoryStream(mzExe);

        var act = async () => await service.UploadAndParseAsync("fake.csv", mzExe.Length, stream);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*executable*");
    }

    [Fact]
    public async Task UploadAndParseAsync_WhenDisguisedPdf_ThrowsArgumentException()
    {
        var service = CreateService();
        // %PDF header
        byte[] pdfHeader = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x35];
        using var stream = new MemoryStream(pdfHeader);

        var act = async () => await service.UploadAndParseAsync("statement.csv", pdfHeader.Length, stream);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*PDF*");
    }

    [Fact]
    public async Task UploadAndParseAsync_WhenFileContainsNullBytes_ThrowsArgumentException()
    {
        var service = CreateService();
        byte[] binaryContent = [0x61, 0x62, 0x00, 0x63]; // contains null byte
        using var stream = new MemoryStream(binaryContent);

        var act = async () => await service.UploadAndParseAsync("statement.csv", binaryContent.Length, stream);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*null bytes*");
    }

    [Fact]
    public async Task UploadAndParseAsync_ValidCsv_SavesStorage_AndPersistsStatement()
    {
        var service = CreateService();
        var csvContent = "Date,Description,Amount\n2026-03-01,Groceries,-50.00";
        var bytes = Encoding.UTF8.GetBytes(csvContent);
        using var stream = new MemoryStream(bytes);

        _statementStorage.SaveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("households/1111/statements/123.csv");

        var parsedRow = new ParsedTransactionRowDto(2, new DateOnly(2026, 3, 1), "Groceries", 50.00m, "Debit", null);
        _csvParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new CsvParseResult(true, 1, [parsedRow], [], "Generic Csv", "{\"totalRows\":1}"));

        var result = await service.UploadAndParseAsync("march-statement.csv", bytes.Length, stream);

        result.Status.Should().Be(StatementProcessingStatus.Parsed);
        result.ParsedTransactionCount.Should().Be(1);
        result.SampleRows.Should().HaveCount(1);
        result.Message.Should().Contain("0 transactions imported");

        await _statementStorage.Received(1).SaveAsync(
            _householdId,
            Arg.Any<Guid>(),
            Arg.Any<Stream>(),
            ".csv",
            Arg.Any<CancellationToken>());

        await _statementRepo.Received(1).AddAsync(
            Arg.Is<Statement>(s => s.HouseholdId == _householdId && s.Status == StatementProcessingStatus.Parsed && s.ParsedTransactionCount == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAndParseAsync_WhenDatabaseFails_CleansUpStoredFile()
    {
        var service = CreateService();
        var csvContent = "Date,Description,Amount\n2026-03-01,Groceries,-50.00";
        var bytes = Encoding.UTF8.GetBytes(csvContent);
        using var stream = new MemoryStream(bytes);

        var fakeKey = "households/1111/statements/rollback.csv";
        _statementStorage.SaveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(fakeKey);

        _csvParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new CsvParseResult(true, 1, [], [], "Generic Csv", null));

        _statementRepo.AddAsync(Arg.Any<Statement>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB connection dropped"));

        var act = async () => await service.UploadAndParseAsync("statement.csv", bytes.Length, stream);

        await act.Should().ThrowAsync<InvalidOperationException>();

        // Verify storage rollback occurred
        await _statementStorage.Received(1).DeleteAsync(fakeKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetStatementsAsync_ReturnsHouseholdScopedStatements()
    {
        var service = CreateService();
        var s1 = new Statement(Guid.NewGuid(), _householdId, _userId, "s1.csv", StatementFormat.Csv, 100, "k1", _fixedNow);
        _statementRepo.GetByHouseholdIdAsync(_householdId, Arg.Any<CancellationToken>())
            .Returns([s1]);

        var result = await service.GetStatementsAsync();

        result.Should().HaveCount(1);
        result[0].OriginalFileName.Should().Be("s1.csv");
    }

    [Fact]
    public async Task GetStatementByIdAsync_WhenBelongsToDifferentHousehold_ReturnsNull()
    {
        var service = CreateService();
        var otherHouseholdId = Guid.NewGuid();
        var sForeign = new Statement(Guid.NewGuid(), otherHouseholdId, "other-user", "foreign.csv", StatementFormat.Csv, 100, "k2", _fixedNow);

        _statementRepo.GetByIdAsync(sForeign.Id, Arg.Any<CancellationToken>())
            .Returns(sForeign);

        var result = await service.GetStatementByIdAsync(sForeign.Id);

        result.Should().BeNull();
    }
}
