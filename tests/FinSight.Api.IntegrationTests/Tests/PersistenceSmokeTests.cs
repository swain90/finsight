using FinSight.Api.IntegrationTests.Helpers;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using FinSight.Domain.ValueObjects;
using FinSight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.Api.IntegrationTests.Tests;

[Collection("Integration")]
public sealed class PersistenceSmokeTests(FinSightWebApplicationFactory factory)
{
    private static DateTimeOffset Utc(DateOnly d) => new(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private async Task<(User User, Account Account)> SeedUserAndAccountAsync(
        AccountType type = AccountType.Checking, string? externalRef = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = User.Create($"persist-{Guid.NewGuid():N}@test.dev", "hash", "Per", "Sist");
        user.ClearDomainEvents();
        var account = Account.Create(user.Id, "Chequing", type, "CAD", "CIBC", Institution.Cibc, AccountOwner.Personal, externalRef);
        account.ClearDomainEvents();
        db.Users.Add(user);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return (user, account);
    }

    private static ImportRow Row(int line, DateOnly on, decimal amount) =>
        ImportRow.Create(line, on, $"Row {line}", "Merchant", new Money(amount, "CAD"),
            TransactionType.Debit, TransactionCategory.Uncategorized, TransactionKind.Regular, $"raw {line}");

    [Fact]
    public async Task ImportBatchWithRowsAndSnapshot_RoundTrips()
    {
        var (user, account) = await SeedUserAndAccountAsync(externalRef: "1234");
        var batchId = Guid.Empty;
        var asOf = new DateOnly(2026, 3, 31);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var batch = ImportBatch.Create(user.Id, Institution.Cibc, "march.csv");
            batch.AddRow(Row(1, new DateOnly(2026, 3, 1), 10.25m));
            batch.AddRow(Row(2, new DateOnly(2026, 3, 2), 20.50m));
            batchId = batch.Id;
            db.ImportBatches.Add(batch);
            var tracked = await db.Accounts.FirstAsync(a => a.Id == account.Id);
            db.BalanceSnapshots.Add(tracked.RecordBalanceSnapshot(asOf, new Money(500m, "CAD"), batch.Id));
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var batches = scope.ServiceProvider.GetRequiredService<IImportBatchRepository>();
            var snapshots = scope.ServiceProvider.GetRequiredService<IBalanceSnapshotRepository>();

            var loaded = await batches.GetByIdAsync(batchId, user.Id);
            Assert.NotNull(loaded);
            Assert.Equal(2, loaded!.Rows.Count);
            Assert.All(loaded.Rows, r => Assert.Equal(batchId, r.ImportBatchId));
            Assert.Equal(new[] { 10.25m, 20.50m }, loaded.Rows.OrderBy(r => r.LineNumber).Select(r => r.Amount.Amount).ToArray());
            Assert.Equal(Institution.Cibc, loaded.Institution);

            var snap = await snapshots.GetLatestAsync(account.Id, null);
            Assert.NotNull(snap);
            Assert.Equal(asOf, snap!.AsOf);
            Assert.Equal(500m, snap.Balance.Amount);

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var acct = await db.Accounts.AsNoTracking().FirstAsync(a => a.Id == account.Id);
            Assert.Equal(asOf, acct.LatestSnapshotAsOf);
            Assert.Equal("1234", acct.ExternalAccountRef);
        }
    }

    [Fact]
    public async Task DuplicateFingerprintSameAccount_ThrowsDbUpdateException()
    {
        var (_, account) = await SeedUserAndAccountAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var acct = await db.Accounts.FirstAsync(a => a.Id == account.Id);
        var batchId = Guid.NewGuid();
        var at = Utc(new DateOnly(2026, 3, 1));
        // Transaction ids are client-generated, so new rows must be added explicitly (as the handlers do
        // via ITransactionRepository.AddRangeAsync); graph discovery alone would mark them Modified.
        db.Transactions.Add(acct.ImportTransaction("A", new Money(1m, "CAD"), TransactionType.Debit, at, TransactionCategory.Uncategorized, TransactionKind.Regular, batchId, "fp-dup"));
        db.Transactions.Add(acct.ImportTransaction("B", new Money(2m, "CAD"), TransactionType.Debit, at, TransactionCategory.Uncategorized, TransactionKind.Regular, batchId, "fp-dup"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task GetExistingFingerprints_ReturnsOnlyMatchesForThatAccount()
    {
        var (_, a1) = await SeedUserAndAccountAsync();
        var (_, a2) = await SeedUserAndAccountAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var x = await db.Accounts.FirstAsync(a => a.Id == a1.Id);
            var y = await db.Accounts.FirstAsync(a => a.Id == a2.Id);
            var at = Utc(new DateOnly(2026, 3, 1));
            var b = Guid.NewGuid();
            db.Transactions.Add(x.ImportTransaction("A", new Money(1m, "CAD"), TransactionType.Debit, at, TransactionCategory.Uncategorized, TransactionKind.Regular, b, "fp-1"));
            db.Transactions.Add(x.ImportTransaction("B", new Money(1m, "CAD"), TransactionType.Debit, at, TransactionCategory.Uncategorized, TransactionKind.Regular, b, "fp-2"));
            db.Transactions.Add(y.ImportTransaction("C", new Money(1m, "CAD"), TransactionType.Debit, at, TransactionCategory.Uncategorized, TransactionKind.Regular, b, "fp-3"));
            await db.SaveChangesAsync();
        }

        using var scope2 = factory.Services.CreateScope();
        var repo = scope2.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var found = await repo.GetExistingFingerprintsAsync(a1.Id, ["fp-1", "fp-3", "fp-missing"]);
        Assert.Equal(new[] { "fp-1" }, found.ToArray());
    }

    [Fact]
    public async Task GetMatchPool_AppliesDateWindowAndExclusions()
    {
        var (user, acct) = await SeedUserAndAccountAsync();
        var (_, otherAcct) = await SeedUserAndAccountAsync();
        var from = new DateOnly(2026, 4, 1);
        var to = new DateOnly(2026, 4, 10);
        var batch = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var a = await db.Accounts.FirstAsync(x => x.Id == acct.Id);
            var o = await db.Accounts.FirstAsync(x => x.Id == otherAcct.Id);

            Transaction Add(Account on, string desc, DateOnly d, TransactionKind kind, string fp)
            {
                var tx = on.ImportTransaction(desc, new Money(5m, "CAD"), TransactionType.Debit, Utc(d),
                    TransactionCategory.Uncategorized, kind, batch, fp);
                db.Transactions.Add(tx);
                return tx;
            }

            Add(a, "in-from", from, TransactionKind.Regular, "m1");
            Add(a, "in-to-day", to, TransactionKind.PendingTransfer, "m2");
            Add(a, "after-to", to.AddDays(1), TransactionKind.Regular, "m3");
            Add(a, "before-from", from.AddDays(-1), TransactionKind.Regular, "m4");
            Add(a, "investment", from.AddDays(2), TransactionKind.InvestmentTrade, "m5");
            var paired = Add(a, "paired", from.AddDays(3), TransactionKind.Regular, "m6");
            paired.MarkPaired(Guid.NewGuid(), TransactionKind.Transfer);
            var dismissed = Add(a, "dismissed", from.AddDays(4), TransactionKind.Regular, "m7");
            dismissed.MarkNotTransfer();
            Add(o, "other-user", from.AddDays(1), TransactionKind.Regular, "m8");
            await db.SaveChangesAsync();
        }

        using var scope2 = factory.Services.CreateScope();
        var repo = scope2.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var pool = await repo.GetMatchPoolAsync(user.Id, from, to);
        Assert.Equal(new[] { "in-from", "in-to-day" }, pool.Select(t => t.Description).OrderBy(d => d).ToArray());
        Assert.All(pool, t => Assert.NotNull(t.Account));
    }

    [Fact]
    public async Task GetPendingTransfers_ReturnsOnlyOwnUnpairedPendingRows()
    {
        var (user, acct) = await SeedUserAndAccountAsync();
        var (_, otherAcct) = await SeedUserAndAccountAsync();
        var batch = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var a = await db.Accounts.FirstAsync(x => x.Id == acct.Id);
            var o = await db.Accounts.FirstAsync(x => x.Id == otherAcct.Id);
            var at = Utc(new DateOnly(2026, 6, 1));

            Transaction Add(Account on, string desc, TransactionKind kind, string fp)
            {
                var tx = on.ImportTransaction(desc, new Money(5m, "CAD"), TransactionType.Debit, at,
                    TransactionCategory.Uncategorized, kind, batch, fp);
                db.Transactions.Add(tx);
                return tx;
            }

            Add(a, "pending", TransactionKind.PendingTransfer, "p1");
            Add(a, "regular", TransactionKind.Regular, "p2");
            Add(o, "someone-else", TransactionKind.PendingTransfer, "p3");
            Add(a, "dismissed", TransactionKind.PendingTransfer, "p4").MarkNotTransfer();
            await db.SaveChangesAsync();
        }

        using var scope2 = factory.Services.CreateScope();
        var repo = scope2.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var pending = await repo.GetPendingTransfersAsync(user.Id);
        Assert.Equal(new[] { "pending" }, pending.Select(t => t.Description).ToArray());
        Assert.All(pending, t => Assert.NotNull(t.Account));
    }

    [Fact]
    public async Task ImportBatchAndTransferQueries_ReturnExpectedRows()
    {
        var (user, acct) = await SeedUserAndAccountAsync();
        var batch = Guid.NewGuid();
        var group = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var a = await db.Accounts.FirstAsync(x => x.Id == acct.Id);
            var at = Utc(new DateOnly(2026, 5, 1));
            var one = a.ImportTransaction("one", new Money(1m, "CAD"), TransactionType.Debit, at, TransactionCategory.Uncategorized, TransactionKind.Regular, batch, "q1");
            one.MarkPaired(group, TransactionKind.Transfer);
            db.Transactions.Add(one);
            db.Transactions.Add(a.ImportTransaction("two", new Money(2m, "CAD"), TransactionType.Debit, at, TransactionCategory.Uncategorized, TransactionKind.Regular, batch, "q2"));
            await db.SaveChangesAsync();
        }

        using var scope2 = factory.Services.CreateScope();
        var repo = scope2.ServiceProvider.GetRequiredService<ITransactionRepository>();
        Assert.Equal(2, (await repo.GetByImportBatchIdAsync(batch)).Count);
        var grouped = await repo.GetByTransferGroupIdsAsync([group]);
        Assert.Single(grouped);
        Assert.Equal("one", grouped[0].Description);
        var ranged = await repo.GetByUserIdAsync(user.Id, Utc(new DateOnly(2026, 5, 1)), Utc(new DateOnly(2026, 5, 2)));
        Assert.All(ranged, t => Assert.NotNull(t.Account));
        Assert.Equal(AccountOwner.Personal, ranged[0].Account.Owner);
    }

    [Fact]
    public async Task MerchantRule_UniquePerUserAndKey()
    {
        var (user, _) = await SeedUserAndAccountAsync();
        using var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IMerchantRuleRepository>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await repo.AddAsync(MerchantRule.Create(user.Id, "netflix", TransactionCategory.Entertainment));
        await db.SaveChangesAsync();
        Assert.Single(await repo.GetByUserIdAsync(user.Id));

        await repo.AddAsync(MerchantRule.Create(user.Id, "netflix", TransactionCategory.Utilities));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Seeder_OnEmptyDatabase_SeedsPairedTransfersAndCorporateAccount()
    {
        using var scope = factory.Services.CreateScope();
        var baseDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(baseDb.Database.GetConnectionString()) { Database = $"seed_{Guid.NewGuid():N}" };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection.ConnectionString).Options;
        await using var db = new ApplicationDbContext(options, scope.ServiceProvider.GetRequiredService<MediatR.IMediator>());
        try
        {
            var seeder = new FinSight.Infrastructure.Persistence.Seeders.DatabaseSeeder(
                db, Microsoft.Extensions.Logging.Abstractions.NullLogger<FinSight.Infrastructure.Persistence.Seeders.DatabaseSeeder>.Instance);
            await seeder.SeedAsync();

            var demo = await db.Users.SingleAsync(u => u.Email == "demo@finsight.dev");
            var accounts = await db.Accounts.Where(a => a.UserId == demo.Id).ToListAsync();
            Assert.Contains(accounts, a => a.Owner == AccountOwner.Corporation && a.Institution == Institution.Rbc);
            Assert.Contains(accounts, a => a.Type == AccountType.CreditCard && a.Institution == Institution.Td);

            var transfers = await db.Transactions.Where(t => t.Kind == TransactionKind.Transfer).ToListAsync();
            Assert.NotEmpty(transfers);
            Assert.All(transfers.GroupBy(t => t.TransferGroupId), g => Assert.Equal(2, g.Count()));
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
