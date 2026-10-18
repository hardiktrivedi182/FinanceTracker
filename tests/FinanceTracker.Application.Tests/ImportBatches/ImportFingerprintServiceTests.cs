using FinanceTracker.Application.ImportBatches.Services;
using FinanceTracker.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FinanceTracker.Application.Tests.ImportBatches;

public class ImportFingerprintServiceTests
{
    private readonly ImportFingerprintService _service = new();
    private readonly Guid _accountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly DateOnly _date = new(2026, 10, 15);

    [Fact]
    public void ComputeFingerprint_IsDeterministic()
    {
        var fp1 = _service.ComputeFingerprint(_accountId, _date, "Whole Foods Market", 54.20m, TransactionType.Debit, "USD");
        var fp2 = _service.ComputeFingerprint(_accountId, _date, "Whole Foods Market", 54.20m, TransactionType.Debit, "USD");

        fp1.Should().Be(fp2);
        fp1.Should().HaveLength(64);
    }

    [Fact]
    public void ComputeFingerprint_NormalizesWhitespaceAndCasing()
    {
        var fp1 = _service.ComputeFingerprint(_accountId, _date, "WHOLE   FOODS   MARKET", 54.20m, TransactionType.Debit, "usd");
        var fp2 = _service.ComputeFingerprint(_accountId, _date, "whole foods market", 54.20m, TransactionType.Debit, "USD");

        fp1.Should().Be(fp2);
    }

    [Fact]
    public void ComputeFingerprint_StripsLeadingFormulaInjectionCharacters()
    {
        var fp1 = _service.ComputeFingerprint(_accountId, _date, "'=PAYROLL DEPOSIT", 1000m, TransactionType.Credit, "USD");
        var fp2 = _service.ComputeFingerprint(_accountId, _date, "PAYROLL DEPOSIT", 1000m, TransactionType.Credit, "USD");

        fp1.Should().Be(fp2);
    }

    [Fact]
    public void ComputeFingerprint_DifferentiatesDirectionAndAmount()
    {
        var fpDebit = _service.ComputeFingerprint(_accountId, _date, "Transfer", 100m, TransactionType.Debit, "USD");
        var fpCredit = _service.ComputeFingerprint(_accountId, _date, "Transfer", 100m, TransactionType.Credit, "USD");
        var fpDifferentAmt = _service.ComputeFingerprint(_accountId, _date, "Transfer", 200m, TransactionType.Debit, "USD");

        fpDebit.Should().NotBe(fpCredit);
        fpDebit.Should().NotBe(fpDifferentAmt);
    }
}
