// =============================================================================
// TransactionTests.cs — Unit Tests for the Transaction Aggregate Root
// =============================================================================
//
// Tests verify:
//   1. Factory method validation (Create enforces all invariants)
//   2. Domain event generation (TransactionCreatedEvent raised on creation)
//   3. State transitions (categorize, mark as anomaly, update notes)
//   4. Business rule enforcement (no future transactions, no zero amounts)
// =============================================================================

using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Events;
using FinSight.Domain.Exceptions;
using FinSight.Domain.ValueObjects;
using FluentAssertions;

namespace FinSight.Domain.Tests.Entities;

/// <summary>
/// Tests for the <see cref="Transaction"/> aggregate root.
/// </summary>
public sealed class TransactionTests
{
    // Shared test fixtures — valid transaction creation parameters
    private static readonly AccountId ValidAccountId  = AccountId.New();
    private const           string    ValidUserId     = "user-abc-123";
    private static readonly Money     ValidAmount     = Money.Of(50.00m, "USD");
    private const           string    ValidDescription = "Starbucks Coffee";
    private static readonly DateTime  ValidDate        = DateTime.UtcNow.AddHours(-1);

    // =========================================================================
    // CREATION — Create() factory method
    // =========================================================================

    [Fact]
    public void Create_ValidParameters_CreatesTransaction()
    {
        // Act
        var transaction = Transaction.Create(
            accountId:    ValidAccountId,
            userId:       ValidUserId,
            amount:       ValidAmount,
            type:         TransactionType.Debit,
            description:  ValidDescription,
            transactedAt: ValidDate);

        // Assert
        transaction.Should().NotBeNull();
        transaction.Id.Should().NotBeNull();
        transaction.Id.Value.Should().NotBe(Guid.Empty);
        transaction.AccountId.Should().Be(ValidAccountId);
        transaction.UserId.Should().Be(ValidUserId);
        transaction.Amount.Should().Be(ValidAmount);
        transaction.Type.Should().Be(TransactionType.Debit);
        transaction.Description.Should().Be(ValidDescription);
        transaction.Category.Should().Be(TransactionCategory.Uncategorized);
        transaction.IsAnomaly.Should().BeFalse();
    }

    [Fact]
    public void Create_RaisesTransactionCreatedDomainEvent()
    {
        // Domain events are critical — they drive cache invalidation, budget updates, etc.
        var transaction = Transaction.Create(
            ValidAccountId, ValidUserId, ValidAmount,
            TransactionType.Debit, ValidDescription, ValidDate);

        // Assert: domain event was raised
        transaction.DomainEvents.Should().HaveCount(1);
        transaction.DomainEvents[0].Should().BeOfType<TransactionCreatedEvent>();

        var createdEvent = (TransactionCreatedEvent)transaction.DomainEvents[0];
        createdEvent.TransactionId.Should().Be(transaction.Id);
        createdEvent.UserId.Should().Be(ValidUserId);
    }

    [Fact]
    public void Create_ZeroAmount_ThrowsDomainException()
    {
        var action = () => Transaction.Create(
            ValidAccountId, ValidUserId,
            Money.Zero("USD"),   // ← zero amount
            TransactionType.Debit, ValidDescription, ValidDate);

        action.Should().Throw<DomainException>()
            .WithMessage("*zero*");
    }

    [Fact]
    public void Create_NegativeAmount_ThrowsDomainException()
    {
        // Negative amounts should NOT be allowed — use TransactionType.Debit instead
        var action = () => Transaction.Create(
            ValidAccountId, ValidUserId,
            Money.Of(-50m, "USD"),   // ← negative amount
            TransactionType.Debit, ValidDescription, ValidDate);

        action.Should().Throw<DomainException>()
            .WithMessage("*positive*");
    }

    [Fact]
    public void Create_FutureDate_ThrowsDomainException()
    {
        // Cannot create transactions dated in the future
        var futureDate = DateTime.UtcNow.AddHours(2);

        var action = () => Transaction.Create(
            ValidAccountId, ValidUserId, ValidAmount,
            TransactionType.Debit, ValidDescription, futureDate);

        action.Should().Throw<DomainException>()
            .WithMessage("*future*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null!)]
    public void Create_EmptyDescription_ThrowsArgumentException(string? description)
    {
        var action = () => Transaction.Create(
            ValidAccountId, ValidUserId, ValidAmount,
            TransactionType.Debit, description!, ValidDate);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_DescriptionIsAutomaticallyTrimmed()
    {
        var transaction = Transaction.Create(
            ValidAccountId, ValidUserId, ValidAmount,
            TransactionType.Debit, "  Starbucks  ", ValidDate);

        transaction.Description.Should().Be("Starbucks");  // Leading/trailing spaces removed
    }

    // =========================================================================
    // CATEGORY MANAGEMENT
    // =========================================================================

    [Fact]
    public void UpdateCategory_ValidCategory_UpdatesCategoryAndRaisesEvent()
    {
        var transaction = CreateValidTransaction();

        transaction.UpdateCategory(TransactionCategory.DiningOut, updatedByUser: true);

        transaction.Category.Should().Be(TransactionCategory.DiningOut);
        transaction.DomainEvents.Should().Contain(e => e is TransactionCategorizedEvent);
    }

    [Fact]
    public void UpdateCategory_SameCategory_StillRaisesEvent()
    {
        // Even if category is the same, we still raise the event
        // (system may need to reprocess)
        var transaction = CreateValidTransaction();
        transaction.ClearDomainEvents();  // Clear creation event

        transaction.UpdateCategory(TransactionCategory.Uncategorized);

        transaction.DomainEvents.Should().HaveCount(1);
    }

    // =========================================================================
    // ANOMALY DETECTION
    // =========================================================================

    [Fact]
    public void MarkAsAnomaly_NotPreviouslyAnomaly_SetsAnomalyAndRaisesEvent()
    {
        var transaction = CreateValidTransaction();
        transaction.ClearDomainEvents();

        transaction.MarkAsAnomaly();

        transaction.IsAnomaly.Should().BeTrue();
        transaction.DomainEvents.Should().Contain(e => e is TransactionAnomalyDetectedEvent);
    }

    [Fact]
    public void MarkAsAnomaly_AlreadyAnomaly_DoesNotRaiseDuplicateEvent()
    {
        // Should be idempotent — marking anomaly twice should not raise 2 events
        var transaction = CreateValidTransaction();
        transaction.MarkAsAnomaly();
        transaction.ClearDomainEvents();

        transaction.MarkAsAnomaly();  // Second call

        transaction.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ClearAnomaly_AnomaliousTransaction_RemovesAnomalyFlag()
    {
        var transaction = CreateValidTransaction();
        transaction.MarkAsAnomaly();

        transaction.ClearAnomaly();

        transaction.IsAnomaly.Should().BeFalse();
    }

    // =========================================================================
    // NOTES
    // =========================================================================

    [Fact]
    public void UpdateNotes_ValidNote_SetsNote()
    {
        var transaction = CreateValidTransaction();
        transaction.UpdateNotes("Business expense");
        transaction.Notes.Should().Be("Business expense");
    }

    [Fact]
    public void UpdateNotes_NullNote_ClearsExistingNote()
    {
        var transaction = CreateValidTransaction();
        transaction.UpdateNotes("Some note");
        transaction.UpdateNotes(null);  // Clear it
        transaction.Notes.Should().BeNull();
    }

    [Fact]
    public void UpdateNotes_WhitespaceIsAutomaticallyTrimmed()
    {
        var transaction = CreateValidTransaction();
        transaction.UpdateNotes("  trimmed note  ");
        transaction.Notes.Should().Be("trimmed note");
    }

    // =========================================================================
    // DOMAIN EVENTS
    // =========================================================================

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var transaction = CreateValidTransaction();
        transaction.DomainEvents.Should().NotBeEmpty();

        transaction.ClearDomainEvents();

        transaction.DomainEvents.Should().BeEmpty();
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    private static Transaction CreateValidTransaction(
        TransactionType type = TransactionType.Debit,
        TransactionCategory category = TransactionCategory.Uncategorized)
        => Transaction.Create(
            ValidAccountId, ValidUserId, ValidAmount,
            type, ValidDescription, ValidDate, category);
}
