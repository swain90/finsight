using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.ValueObjects;
using FluentAssertions;

namespace FinSight.Domain.Tests.Entities;

public sealed class TransactionTests
{
    private static Transaction NewDebit(TransactionCategory category = TransactionCategory.Uncategorized)
    {
        var account = Account.Create(Guid.NewGuid(), "Chequing", AccountType.Checking, "CAD");
        return account.RecordTransaction(
            "  Loblaws  ", Money.CAD(82.15m), TransactionType.Debit,
            DateTimeOffset.UtcNow.AddHours(-1), category, "  Loblaws #123 ");
    }

    [Fact]
    public void RecordTransaction_TrimsDescriptionAndMerchant()
    {
        var tx = NewDebit();

        tx.Description.Should().Be("Loblaws");
        tx.MerchantName.Should().Be("Loblaws #123");
        tx.Amount.Should().Be(Money.CAD(82.15m));
        tx.Category.Should().Be(TransactionCategory.Uncategorized);
        tx.IsAnomaly.Should().BeFalse();
    }

    [Fact]
    public void Categorize_SetsCategory()
    {
        var tx = NewDebit();

        tx.Categorize(TransactionCategory.GroceriesAndFood);

        tx.Category.Should().Be(TransactionCategory.GroceriesAndFood);
    }

    [Fact]
    public void AddNotes_TrimsNotes()
    {
        var tx = NewDebit();

        tx.AddNotes("  weekly shop ");

        tx.Notes.Should().Be("weekly shop");
    }

    [Fact]
    public void LinkToPlaid_StoresExternalId()
    {
        var tx = NewDebit();

        tx.LinkToPlaid("plaid-tx-1");

        tx.PlaidTransactionId.Should().Be("plaid-tx-1");
    }
}
