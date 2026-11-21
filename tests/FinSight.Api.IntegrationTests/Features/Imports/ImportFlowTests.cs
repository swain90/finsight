using System.Text;
using FinSight.Api.IntegrationTests.Helpers;
using FinSight.Application.Features.Imports.Commands.CommitImport;
using FinSight.Application.Features.Imports.Commands.CreateImport;
using FinSight.Application.Features.Imports.Commands.DeleteImport;
using FinSight.Application.Features.Imports.Common;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinSight.Api.IntegrationTests.Features.Imports;

/// <summary>Drives the import flow through MediatR against a real PostgreSQL database.</summary>
[Collection("Integration")]
public sealed class ImportFlowTests(FinSightWebApplicationFactory factory) : IClassFixture<FinSightWebApplicationFactory>, IDisposable
{
    /// <summary>The acting-user override is static; clear it so it cannot leak into later tests.</summary>
    public void Dispose() => TestCurrentUserService.Override = null;

    private static readonly DateOnly AsOf = new(2026, 1, 31);

    private async Task<Guid> NewUserAsync()
    {
        var email = $"import-{Guid.NewGuid():N}@test.dev";
        await factory.CreateAuthenticatedClientAsync(email);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var id = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
        TestCurrentUserService.Override = id;
        return id;
    }

    private async Task<Guid> NewAccountAsync(Guid userId, string name, AccountType type)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = Account.Create(userId, name, type, "CAD", null, Institution.Other, AccountOwner.Personal, null);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task<ImportBatchDto> UploadAsync(Guid accountId, params string[] lines)
    {
        var csv = "Date,Description,Amount\n" + string.Join('\n', lines) + "\n";
        var bytes = Encoding.UTF8.GetBytes(csv);
        var result = await SendAsync(new CreateImportCommand("statement.csv", bytes.Length, new MemoryStream(bytes), accountId));
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : "");
        return result.Value;
    }

    private async Task CommitAsync(Guid batchId, params ClosingBalanceInput[] balances)
    {
        var result = await SendAsync(new CommitImportCommand(batchId, balances));
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : "");
    }

    private async Task<List<Transaction>> TransactionsAsync(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Transactions.AsNoTracking()
            .Where(t => t.Account.UserId == userId)
            .OrderBy(t => t.OccurredAt).ThenBy(t => t.Description)
            .ToListAsync();
    }

    [Fact]
    public async Task Commit_SameFileTwice_SecondCommitAddsNothing()
    {
        var userId = await NewUserAsync();
        var account = await NewAccountAsync(userId, "Chequing", AccountType.Checking);
        string[] lines = ["2026-01-05,COFFEE SHOP,-4.50", "2026-01-06,PAYROLL,2000.00"];

        var first = await UploadAsync(account, lines);
        await CommitAsync(first.Id, new ClosingBalanceInput(account, 1995.50m, AsOf));
        (await TransactionsAsync(userId)).Should().HaveCount(2);

        var second = await UploadAsync(account, lines);
        second.Rows.Should().OnlyContain(r => r.IsDuplicate && r.IsExcluded);
        await CommitAsync(second.Id);

        (await TransactionsAsync(userId)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Commit_IdenticalSameDayRows_BothKept()
    {
        var userId = await NewUserAsync();
        var account = await NewAccountAsync(userId, "Chequing", AccountType.Checking);

        var batch = await UploadAsync(account, "2026-01-05,COFFEE SHOP,-4.50", "2026-01-05,COFFEE SHOP,-4.50");
        await CommitAsync(batch.Id, new ClosingBalanceInput(account, 91m, AsOf));

        var txs = await TransactionsAsync(userId);
        txs.Should().HaveCount(2);
        txs.Select(t => t.ImportFingerprint).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task Commit_PartnerFileLater_PairsPendingTransfer()
    {
        var userId = await NewUserAsync();
        var savings = await NewAccountAsync(userId, "Savings", AccountType.Savings);
        var card = await NewAccountAsync(userId, "Card", AccountType.CreditCard);

        var savingsBatch = await UploadAsync(savings, "2026-01-05,E TRANSFER TO CARD,-100.00");
        await CommitAsync(savingsBatch.Id, new ClosingBalanceInput(savings, 900m, AsOf));
        (await TransactionsAsync(userId)).Single().Kind.Should().Be(TransactionKind.PendingTransfer);

        var cardBatch = await UploadAsync(card, "2026-01-07,PAYMENT THANK YOU,100.00");
        await CommitAsync(cardBatch.Id, new ClosingBalanceInput(card, 400m, AsOf));

        var txs = await TransactionsAsync(userId);
        txs.Should().HaveCount(2).And.OnlyContain(t => t.Kind == TransactionKind.Transfer);
        txs[0].TransferGroupId.Should().NotBeNull().And.Be(txs[1].TransferGroupId);
    }

    [Fact]
    public async Task Undo_PairedBatch_PartnerBecomesPendingTransfer()
    {
        var userId = await NewUserAsync();
        var savings = await NewAccountAsync(userId, "Savings", AccountType.Savings);
        var card = await NewAccountAsync(userId, "Card", AccountType.CreditCard);
        var savingsBatch = await UploadAsync(savings, "2026-01-05,E TRANSFER TO CARD,-100.00");
        await CommitAsync(savingsBatch.Id, new ClosingBalanceInput(savings, 900m, AsOf));
        var cardBatch = await UploadAsync(card, "2026-01-07,PAYMENT THANK YOU,100.00");
        await CommitAsync(cardBatch.Id, new ClosingBalanceInput(card, 400m, AsOf));

        var result = await SendAsync(new DeleteImportCommand(cardBatch.Id));

        result.IsSuccess.Should().BeTrue();
        var remaining = (await TransactionsAsync(userId)).Single();
        remaining.AccountId.Should().Be(savings);
        remaining.Kind.Should().Be(TransactionKind.PendingTransfer);
        remaining.TransferGroupId.Should().BeNull();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cardAccount = await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == card);
        cardAccount.Balance.Amount.Should().Be(0m);
        (await db.BalanceSnapshots.CountAsync(s => s.AccountId == card)).Should().Be(0);
        var savingsAccount = await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == savings);
        savingsAccount.Balance.Amount.Should().Be(900m);
        (await db.ImportBatches.AsNoTracking().SingleAsync(b => b.Id == cardBatch.Id)).Status
            .Should().Be(ImportBatchStatus.Discarded);
    }

    [Fact]
    public async Task Commit_TwoPendingBatchesOfSameFile_SecondFailsWithAlreadyImported()
    {
        var userId = await NewUserAsync();
        var account = await NewAccountAsync(userId, "Chequing", AccountType.Checking);
        string[] lines = ["2026-01-05,COFFEE SHOP,-4.50", "2026-01-06,PAYROLL,2000.00"];
        var first = await UploadAsync(account, lines);
        var second = await UploadAsync(account, lines);
        second.Rows.Should().OnlyContain(r => !r.IsDuplicate);
        await CommitAsync(first.Id, new ClosingBalanceInput(account, 1995.50m, AsOf));

        var failed = await SendAsync(new CommitImportCommand(second.Id, [new ClosingBalanceInput(account, 1995.50m, AsOf)]));

        failed.IsFailure.Should().BeTrue();
        failed.Error.Code.Should().Be("Import.AlreadyImported");
        (await TransactionsAsync(userId)).Should().HaveCount(2);
        var reloaded = await SendAsync(new FinSight.Application.Features.Imports.Queries.GetImport.GetImportQuery(second.Id));
        reloaded.Value.Status.Should().Be("PendingReview");
        reloaded.Value.Rows.Should().OnlyContain(r => r.IsDuplicate && r.IsExcluded);

        await CommitAsync(second.Id);
        (await TransactionsAsync(userId)).Should().HaveCount(2);
    }
}
