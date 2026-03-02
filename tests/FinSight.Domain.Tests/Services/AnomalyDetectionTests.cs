using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace FinSight.Domain.Tests.Services;

public sealed class AnomalyDetectionDomainTests
{
    [Fact]
    public void MarkAsAnomaly_SetsIsAnomalyTrue()
    {
        var account = Account.Create(Guid.NewGuid(), "Test", AccountType.Checking, "CAD");
        var tx = account.RecordTransaction("Test", Money.CAD(50), TransactionType.Debit, DateTimeOffset.UtcNow);

        tx.IsAnomaly.Should().BeFalse();
        tx.MarkAsAnomaly();
        tx.IsAnomaly.Should().BeTrue();
    }

    [Fact]
    public void MarkAsAnomaly_CalledTwice_RemainsTrue()
    {
        var account = Account.Create(Guid.NewGuid(), "Test", AccountType.Checking, "CAD");
        var tx = account.RecordTransaction("Test", Money.CAD(50), TransactionType.Debit, DateTimeOffset.UtcNow);
        tx.MarkAsAnomaly();
        tx.MarkAsAnomaly();
        tx.IsAnomaly.Should().BeTrue();
    }

    [Fact]
    public void NewTransaction_IsNotAnomaly()
    {
        var account = Account.Create(Guid.NewGuid(), "Test", AccountType.Checking, "CAD");
        var tx = account.RecordTransaction("Normal", Money.CAD(25), TransactionType.Debit, DateTimeOffset.UtcNow);
        tx.IsAnomaly.Should().BeFalse();
    }
}
