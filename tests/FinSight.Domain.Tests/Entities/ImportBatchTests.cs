using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Exceptions;
using FinSight.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace FinSight.Domain.Tests.Entities;

public class ImportBatchTests
{
    private static ImportBatch Batch() => ImportBatch.Create(Guid.NewGuid(), Institution.Cibc, "oct.csv");

    private static ImportRow Row(int line, DateOnly on, string raw = "raw") =>
        ImportRow.Create(line, on, "LOBLAWS", null, Money.CAD(10m), TransactionType.Debit,
            TransactionCategory.GroceriesAndFood, TransactionKind.Regular, raw);

    [Fact]
    public void AddRow_WidensPeriod()
    {
        var b = Batch();
        b.AddRow(Row(1, new DateOnly(2026, 10, 28)));
        b.AddRow(Row(2, new DateOnly(2026, 10, 3)));
        b.PeriodStart.Should().Be(new DateOnly(2026, 10, 3));
        b.PeriodEnd.Should().Be(new DateOnly(2026, 10, 28));
        b.Rows.Should().HaveCount(2);
    }

    [Fact]
    public void ApplyStatementSummary_SetsBalancesAndPeriodEnd()
    {
        var b = Batch();
        b.AddRow(Row(1, new DateOnly(2026, 9, 26)));
        b.AddRow(Row(2, new DateOnly(2026, 10, 29)));

        b.ApplyStatementSummary(new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 29), 120.00m, 208.40m);

        b.StatementOpeningBalance.Should().Be(120.00m);
        b.StatementClosingBalance.Should().Be(208.40m);
        b.PeriodStart.Should().Be(new DateOnly(2026, 9, 26));
        b.PeriodEnd.Should().Be(new DateOnly(2026, 10, 29));
    }

    [Fact]
    public void ApplyStatementSummary_NoRows_UsesSummaryPeriod()
    {
        var b = Batch();
        b.ApplyStatementSummary(new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 29), 0m, 0m);
        (b.PeriodStart, b.PeriodEnd).Should().Be((new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 29)));
    }

    [Fact]
    public void Commit_FromPending_SetsCommittedAndWarning()
    {
        var b = Batch();
        b.Commit("off by 5");
        (b.Status, b.ReconciliationWarning).Should().Be((ImportBatchStatus.Committed, "off by 5"));
        b.CommittedAt.Should().NotBeNull();
    }

    [Fact]
    public void Commit_Twice_Throws()
    {
        var b = Batch();
        b.Commit(null);
        var act = () => b.Commit(null);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Discard_Committed_Throws()
    {
        var b = Batch();
        b.Commit(null);
        var act = () => b.Discard();
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void MarkUndone_Committed_SetsDiscarded()
    {
        var b = Batch();
        b.Commit(null);
        b.MarkUndone();
        b.Status.Should().Be(ImportBatchStatus.Discarded);
    }

    [Fact]
    public void AssignAccount_Duplicate_ExcludesRow()
    {
        var r = Row(1, new DateOnly(2026, 10, 3));
        var acct = Guid.NewGuid();
        r.AssignAccount(acct, "fp", true);
        (r.AccountId, r.Fingerprint, r.IsDuplicate, r.IsExcluded).Should().Be((acct, "fp", true, true));
    }

    [Fact]
    public void Create_LongRawLine_TruncatesTo1000()
    {
        Row(1, new DateOnly(2026, 10, 3), new string('x', 2000)).RawLine.Should().HaveLength(1000);
    }

    [Fact]
    public void AssignAccount_BecomingDuplicate_ExcludesRow()
    {
        var row = Row(2, new DateOnly(2026, 1, 5));

        row.AssignAccount(Guid.NewGuid(), "fp", true);

        row.IsDuplicate.Should().BeTrue();
        row.IsExcluded.Should().BeTrue();
    }

    [Fact]
    public void AssignAccount_NoLongerDuplicate_IncludesRow()
    {
        var row = Row(2, new DateOnly(2026, 1, 5));
        row.AssignAccount(Guid.NewGuid(), "fp", true);

        row.AssignAccount(Guid.NewGuid(), "fp2", false);

        row.IsDuplicate.Should().BeFalse();
        row.IsExcluded.Should().BeFalse();
    }

    [Fact]
    public void AssignAccount_NonDuplicateToNonDuplicate_KeepsManualExclusion()
    {
        var row = Row(2, new DateOnly(2026, 1, 5));
        row.SetExcluded(true);

        row.AssignAccount(Guid.NewGuid(), "fp", false);

        row.IsExcluded.Should().BeTrue();
    }
}
