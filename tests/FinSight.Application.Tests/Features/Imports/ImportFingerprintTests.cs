using FinSight.Application.Features.Imports.Common;
using FluentAssertions;

namespace FinSight.Application.Tests.Features.Imports;

public class ImportFingerprintTests
{
    [Theory]
    [InlineData("Loblaws #1234  Toronto", "LOBLAWS TORONTO")]
    [InlineData("TIM HORTONS 0456", "TIM HORTONS")]
    [InlineData("E-TRANSFER  ***ab9", "E TRANSFER AB9")]
    [InlineData("7-ELEVEN 23", "7 ELEVEN 23")]
    public void Normalize_ProducesStableKey(string input, string expected) =>
        DescriptionNormalizer.Normalize(input).Should().Be(expected);

    [Fact]
    public void Compute_SameInputs_SameFingerprint()
    {
        var accountId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 1);

        var first = ImportFingerprint.Compute(accountId, date, 4.50m, TransactionType.Debit, "TIM HORTONS", 0);
        var second = ImportFingerprint.Compute(accountId, date, 4.50m, TransactionType.Debit, "TIM HORTONS", 0);

        first.Should().Be(second);
        first.Should().HaveLength(64);
        first.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Compute_IdenticalRowsDifferentOccurrence_DifferentFingerprints()
    {
        var accountId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 1);

        var first = ImportFingerprint.Compute(accountId, date, 4.50m, TransactionType.Debit, "TIM HORTONS", 0);
        var second = ImportFingerprint.Compute(accountId, date, 4.50m, TransactionType.Debit, "TIM HORTONS", 1);

        first.Should().NotBe(second);
    }

    [Fact]
    public void OccurrenceIndexes_TwoIdenticalCoffees_Returns0And1()
    {
        var date = new DateOnly(2026, 11, 1);
        var rows = new List<(DateOnly Date, decimal Amount, TransactionType Type, string NormalizedDescription)>
        {
            (date, 4.50m, TransactionType.Debit, "TIM HORTONS"),
            (date, 12.00m, TransactionType.Debit, "LOBLAWS"),
            (date, 4.50m, TransactionType.Debit, "TIM HORTONS"),
        };

        var indexes = ImportFingerprint.OccurrenceIndexes(rows);

        indexes.Should().Equal(0, 0, 1);
    }
}
