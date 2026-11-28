using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Budgets.Queries.GetBudgetSummary;
using FinSight.Application.Tests.Common;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using FinSight.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace FinSight.Application.Tests.Features.Budgets;

public sealed class GetBudgetSummaryQueryHandlerTests : HandlerTestBase
{
    private readonly IBudgetRepository _budgets = Substitute.For<IBudgetRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();

    private GetBudgetSummaryQueryHandler CreateSut() => new(_budgets, _transactions, _currentUser);

    [Fact]
    public async Task Handle_WithBudgets_ShouldComputeCorrectTotals()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(userId);

        var budgets = new List<Budget>
        {
            CreateBudgetWithSpending(userId, TransactionCategory.GroceriesAndFood, limit: 500m, spent: 350m),
            CreateBudgetWithSpending(userId, TransactionCategory.Transportation, limit: 200m, spent: 250m),
        };

        _budgets.GetByUserIdAsync(userId, 6, 2025, Arg.Any<CancellationToken>())
            .Returns(budgets.AsReadOnly());

        // Spending is derived from the month's debit transactions, not budget counters.
        var account = CreateAccount(userId);
        var june = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var transactions = new List<Transaction>
        {
            account.RecordTransaction("Loblaws", CAD(350m), TransactionType.Debit, june, TransactionCategory.GroceriesAndFood),
            account.RecordTransaction("Presto", CAD(250m), TransactionType.Debit, june, TransactionCategory.Transportation),
        };
        _transactions.GetByUserIdAsync(userId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(transactions.AsReadOnly());

        // Act
        var result = await CreateSut().Handle(new GetBudgetSummaryQuery(6, 2025), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var summary = result.Value;

        summary.TotalBudgets.Should().Be(2);
        summary.TotalBudgeted.Should().Be(700m);
        summary.TotalSpent.Should().Be(600m);
        summary.TotalRemaining.Should().Be(100m);
        summary.BudgetsOnTrack.Should().Be(1);         // Food is under budget
        summary.BudgetsOverLimit.Should().Be(1);        // Transportation is over
        summary.OverallSpendingPercentage.Should().BeApproximately(85.7m, 0.1m);

        summary.Alerts.Should().HaveCount(1);
        summary.Alerts[0].Category.Should().Be("Transportation");
        summary.Alerts[0].OverageAmount.Should().Be(50m);
        summary.Alerts[0].OveragePercentage.Should().Be(25.0m);
    }

    [Fact]
    public async Task BudgetSummary_RefundReducesCategorySpent()
    {
        var userId = Guid.NewGuid();
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(userId);
        _budgets.GetByUserIdAsync(userId, 6, 2025, Arg.Any<CancellationToken>())
            .Returns(new List<Budget>
            {
                Budget.Create(userId, TransactionCategory.Entertainment, new Money(200m, "CAD"), 6, 2025)
            }.AsReadOnly());

        var account = CreateAccount(userId);
        var june = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var transactions = new List<Transaction>
        {
            account.RecordTransaction("Concert", CAD(100m), TransactionType.Debit, june, TransactionCategory.Entertainment),
            account.RecordTransaction("Concert refund", CAD(30m), TransactionType.Credit, june, TransactionCategory.Entertainment),
        };
        _transactions.GetByUserIdAsync(userId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(transactions.AsReadOnly());

        var result = await CreateSut().Handle(new GetBudgetSummaryQuery(6, 2025), CancellationToken.None);

        result.Value.TotalSpent.Should().Be(70m);
    }

    [Fact]
    public async Task Handle_NoBudgets_ShouldReturnEmptySummary()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(userId);
        _budgets.GetByUserIdAsync(userId, 6, 2025, Arg.Any<CancellationToken>())
            .Returns(new List<Budget>().AsReadOnly());

        // Act
        var result = await CreateSut().Handle(new GetBudgetSummaryQuery(6, 2025), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalBudgets.Should().Be(0);
        result.Value.Alerts.Should().BeEmpty();
    }

    private static Budget CreateBudgetWithSpending(Guid userId, TransactionCategory category, decimal limit, decimal spent)
    {
        var budget = Budget.Create(userId, category, new Money(limit, "CAD"), 6, 2025);
        budget.AddSpending(new Money(spent, "CAD"));
        return budget;
    }
}
