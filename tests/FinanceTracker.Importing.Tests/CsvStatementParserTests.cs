using System.Text;
using FinanceTracker.Importing.Csv;
using FluentAssertions;
using Xunit;

namespace FinanceTracker.Importing.Tests;

public class CsvStatementParserTests
{
    private readonly CsvStatementParser _parser = new();

    [Fact]
    public async Task ParseAsync_EmptyStream_ReturnsError()
    {
        using var stream = new MemoryStream();
        var result = await _parser.ParseAsync(stream);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("empty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ParseAsync_MissingRequiredColumns_ReturnsUnsupportedFormat()
    {
        var csv = "ColA,ColB,ColC\r\n1,2,3";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await _parser.ParseAsync(stream);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("Unsupported CSV format", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ParseAsync_GenericCsvWithDebitCreditColumns_ParsesCorrectly()
    {
        var csv = """
            Date,Description,Debit,Credit,Balance
            2026-03-01,Grocery Store,45.50,,1200.00
            2026-03-02,Salary,,3500.00,4700.00
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await _parser.ParseAsync(stream);

        result.IsSuccess.Should().BeTrue();
        result.Rows.Should().HaveCount(2);

        var row1 = result.Rows[0];
        row1.RowNumber.Should().Be(2);
        row1.Date.Should().Be(new DateOnly(2026, 3, 1));
        row1.Description.Should().Be("Grocery Store");
        row1.Direction.Should().Be("Debit");
        row1.Amount.Should().Be(45.50m);
        row1.Balance.Should().Be(1200.00m);

        var row2 = result.Rows[1];
        row2.RowNumber.Should().Be(3);
        row2.Date.Should().Be(new DateOnly(2026, 3, 2));
        row2.Description.Should().Be("Salary");
        row2.Direction.Should().Be("Credit");
        row2.Amount.Should().Be(3500.00m);
        row2.Balance.Should().Be(4700.00m);
    }

    [Fact]
    public async Task ParseAsync_SingleAmountColumnWithSigns_ParsesDebitAndCredit()
    {
        var csv = """
            Date,Description,Amount,Balance
            15/03/2026,Electric Utility,-120.75,879.25
            16/03/2026,Dividend Payment,50.00,929.25
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await _parser.ParseAsync(stream);

        result.IsSuccess.Should().BeTrue();
        result.Rows.Should().HaveCount(2);

        result.Rows[0].Direction.Should().Be("Debit");
        result.Rows[0].Amount.Should().Be(120.75m);

        result.Rows[1].Direction.Should().Be("Credit");
        result.Rows[1].Amount.Should().Be(50.00m);
    }

    [Fact]
    public async Task ParseAsync_Rfc4180QuotedFieldsWithCommasAndEscapedQuotes_ParsesAccurately()
    {
        var csv = "Date,Description,Amount\r\n2026-04-10,\"Bookstore, \"\"Downtown Branch\"\"\",-29.99";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await _parser.ParseAsync(stream);

        result.IsSuccess.Should().BeTrue();
        result.Rows.Should().HaveCount(1);
        result.Rows[0].Description.Should().Be("Bookstore, \"Downtown Branch\"");
        result.Rows[0].Amount.Should().Be(29.99m);
        result.Rows[0].Direction.Should().Be("Debit");
    }

    [Fact]
    public async Task ParseAsync_FormulaInjectionInDescription_SanitizesSafely()
    {
        var csv = """
            Date,Description,Amount
            2026-05-01,=1+1,-10.00
            2026-05-02,@SUM(A1:A5),-20.00
            2026-05-03,+cmd|' /C calc'!A0,-30.00
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await _parser.ParseAsync(stream);

        result.IsSuccess.Should().BeTrue();
        result.Rows[0].Description.Should().Be("'=1+1");
        result.Rows[1].Description.Should().Be("'@SUM(A1:A5)");
        result.Rows[2].Description.Should().Be("'+cmd|' /C calc'!A0");
    }

    [Fact]
    public async Task ParseAsync_AmbiguousDebitAndCreditOnSameRow_ReportsError()
    {
        var csv = """
            Date,Description,Debit,Credit
            2026-06-01,Transfer,100.00,200.00
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await _parser.ParseAsync(stream);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.RowNumber == 2 && e.Message.Contains("Ambiguous", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ParseAsync_InvalidDateFormat_ReportsRowError()
    {
        var csv = """
            Date,Description,Amount
            not-a-date,Store Purchase,-15.00
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await _parser.ParseAsync(stream);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.RowNumber == 2 && e.Message.Contains("Invalid date", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ParseAsync_InvalidAmountFormat_ReportsRowError()
    {
        var csv = """
            Date,Description,Amount
            2026-07-01,Store Purchase,N/A
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await _parser.ParseAsync(stream);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.RowNumber == 2 && e.Message.Contains("Invalid transaction amount", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ParseAsync_WithUtf8Bom_ParsesHeadersCorrectly()
    {
        var csv = "Date,Description,Amount\r\n2026-08-01,Coffee,-4.50";
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();

        using var stream = new MemoryStream(bytes);
        var result = await _parser.ParseAsync(stream);

        result.IsSuccess.Should().BeTrue();
        result.Rows.Should().HaveCount(1);
        result.Rows[0].Description.Should().Be("Coffee");
    }
}
