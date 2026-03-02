using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Transactions.Commands.RunAnomalyDetection;
using FinSight.Domain.Common;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using FinSight.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace FinSight.Application.Tests.Features.Transactions;

public sealed class RunAnomalyDetectionCommandHandlerTests
{
    private readonly ITransactionRepository _txRepo = Substitute.For<ITransactionRepository>();
    private readonly IAnomalyDetectionService _detectionService = Substitute.For<IAnomalyDetectionService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<RunAnomalyDetectionCommandHandler> _logger =
        Substitute.For<ILogger<RunAnomalyDetectionCommandHandler>>();

    private RunAnomalyDetectionCommandHandler CreateSut() =>
        new(_txRepo, _detectionService, _unitOfWork, _logger);

    [Fact]
    public async Task Handle_WithInsufficientTransactions_SkipsDetection()
    {
        // Arrange — fewer than 5 transactions
        var userId = Guid.NewGuid();
        var fewTransactions = Enumerable.Range(0, 3)
            .Select(_ => MakeTransaction())
            .ToList()
            .AsReadOnly() as IReadOnlyList<Transaction>;

        _txRepo.GetByUserIdAsync(userId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(fewTransactions!);

        // Act
        var result = await CreateSut().Handle(new RunAnomalyDetectionCommand(userId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Detection service should NOT have been called
        await _detectionService.DidNotReceive()
            .DetectAnomaliesAsync(Arg.Any<IReadOnlyList<Transaction>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAnomaliesDetected_MarksTransactionsAndSaves()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var transactions = Enumerable.Range(0, 10)
            .Select(_ => MakeTransaction())
            .ToList()
            .AsReadOnly() as IReadOnlyList<Transaction>;

        _txRepo.GetByUserIdAsync(userId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(transactions!);

        // Anomaly service flags the first two
        var anomalousIds = new[] { transactions![0].Id, transactions[1].Id }
            .ToList()
            .AsReadOnly() as IReadOnlyList<Guid>;

        _detectionService.DetectAnomaliesAsync(Arg.Any<IReadOnlyList<Transaction>>(), Arg.Any<CancellationToken>())
            .Returns(anomalousIds!);

        // Act
        var result = await CreateSut().Handle(new RunAnomalyDetectionCommand(userId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        transactions![0].IsAnomaly.Should().BeTrue();
        transactions[1].IsAnomaly.Should().BeTrue();
        transactions[2].IsAnomaly.Should().BeFalse();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoAnomaliesDetected_DoesNotSave()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var transactions = Enumerable.Range(0, 10)
            .Select(_ => MakeTransaction())
            .ToList()
            .AsReadOnly() as IReadOnlyList<Transaction>;

        _txRepo.GetByUserIdAsync(userId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(transactions!);

        // No anomalies found
        _detectionService.DetectAnomaliesAsync(Arg.Any<IReadOnlyList<Transaction>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Guid>() as IReadOnlyList<Guid>);

        // Act
        await CreateSut().Handle(new RunAnomalyDetectionCommand(userId), CancellationToken.None);

        // Assert: no changes = no SaveChanges call (avoids unnecessary DB round-trips)
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static Transaction MakeTransaction()
    {
        var account = Account.Create(Guid.NewGuid(), "Test", AccountType.Checking, "CAD");
        return account.RecordTransaction(
            "Test Transaction",
            Money.CAD(100),
            TransactionType.Debit,
            DateTimeOffset.UtcNow,
            TransactionCategory.Food);
    }
}
