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
    public void Compute_FixedInput_MatchesGoldenValue()
    {
        // Pinned: stored fingerprints (unique per account) depend on this exact format. Changing it would
        // make every re-import look new. Expected = SHA-256 of
        // "0f8fad5bd9cb469fa16570867728950e|2026-11-01|4.50|Debit|TIM HORTONS|0", lowercase hex.
        var fingerprint = ImportFingerprint.Compute(
            Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"), new DateOnly(2026, 11, 1), 4.5m,
            TransactionType.Debit, "TIM HORTONS", 0);

        fingerprint.Should().Be("b166790d7aa5e73d1562d622b9d3d18b33bbed10b7929b202e0df91e3cdc5db6");
    }

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
