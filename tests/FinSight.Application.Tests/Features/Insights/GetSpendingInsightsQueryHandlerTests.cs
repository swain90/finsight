using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Insights.Queries.GetSpendingInsights;
using FinSight.Application.Tests.Common;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace FinSight.Application.Tests.Features.Insights;

public sealed class GetSpendingInsightsQueryHandlerTests : HandlerTestBase
{
    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IAiInsightsService _ai = Substitute.For<IAiInsightsService>();

    [Fact]
    public async Task SpendingInsights_ExcludesTransfers()
    {
        var userId = Guid.NewGuid();
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(userId);
        _ai.GenerateSpendingInsightAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("ok");

        var account = CreateAccount(userId);
        var june = new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero);
        var groceries = account.RecordTransaction("Loblaws", CAD(100m), TransactionType.Debit, june, TransactionCategory.GroceriesAndFood);
        var refund = account.RecordTransaction("Refund", CAD(30m), TransactionType.Credit, june, TransactionCategory.GroceriesAndFood);
        var transfer = account.ImportTransaction("To savings", CAD(500m), TransactionType.Debit, june,
            TransactionCategory.Other, TransactionKind.PendingTransfer, Guid.NewGuid(), "fp1");
        var pay = account.ImportTransaction("Corp pay", CAD(3000m), TransactionType.Credit, june,
            TransactionCategory.Other, TransactionKind.PendingTransfer, Guid.NewGuid(), "fp2");
        pay.MarkPaired(Guid.NewGuid(), TransactionKind.OwnerPay, TransactionCategory.PayFromCorporation);
        transfer.MarkPaired(Guid.NewGuid(), TransactionKind.Transfer);

        _transactions.GetByUserIdAsync(userId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new List<Transaction> { groceries, refund, transfer, pay }.AsReadOnly());

        var sut = new GetSpendingInsightsQueryHandler(_transactions, _currentUser, _ai);
        var result = await sut.Handle(new GetSpendingInsightsQuery(6, 2025), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalSpent.Should().Be(70m);
        result.Value.TotalIncome.Should().Be(3000m);
        result.Value.CategoryBreakdown.Should().ContainSingle().Which.Amount.Should().Be(70m);
    }
}
