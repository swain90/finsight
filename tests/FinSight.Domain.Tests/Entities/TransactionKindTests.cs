using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Exceptions;
using FinSight.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace FinSight.Domain.Tests.Entities;

public class TransactionKindTests
{
    private static Transaction NewTx() =>
        Account.Create(Guid.NewGuid(), "Chequing", AccountType.Checking)
            .RecordTransaction("Pay", Money.CAD(10m), TransactionType.Credit, DateTimeOffset.UtcNow);

    [Fact]
    public void MarkPaired_OwnerPay_SetsGroupKindCategory()
    {
        var tx = NewTx();
        var group = Guid.NewGuid();
        tx.MarkPaired(group, TransactionKind.OwnerPay, TransactionCategory.PayFromCorporation);
        (tx.TransferGroupId, tx.Kind, tx.Category)
            .Should().Be((group, TransactionKind.OwnerPay, TransactionCategory.PayFromCorporation));
    }

    [Fact]
    public void MarkPaired_RegularKind_Throws()
    {
        var tx = NewTx();
        var act = () => tx.MarkPaired(Guid.NewGuid(), TransactionKind.Regular);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void MarkNotTransfer_SetsRegularAndDismissed()
    {
        var tx = NewTx();
        tx.MarkPendingTransfer();
        tx.MarkNotTransfer();
        (tx.Kind, tx.TransferGroupId, tx.IsTransferDismissed).Should().Be((TransactionKind.Regular, (Guid?)null, true));
    }

    [Fact]
    public void Unpair_SetsPendingAndClearsGroup()
    {
        var tx = NewTx();
        tx.MarkPaired(Guid.NewGuid(), TransactionKind.Transfer);
        tx.Unpair();
        (tx.Kind, tx.TransferGroupId).Should().Be((TransactionKind.PendingTransfer, (Guid?)null));
    }

    [Theory]
    [InlineData(TransactionCategory.PayFromCorporation, true)]
    [InlineData(TransactionCategory.Salary, true)]
    [InlineData(TransactionCategory.DiningOut, false)]
    public void IsIncome_MatchesSpec(TransactionCategory c, bool expected) => c.IsIncome().Should().Be(expected);
}
