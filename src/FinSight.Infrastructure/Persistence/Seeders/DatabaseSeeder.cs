using Bogus;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinSight.Infrastructure.Persistence.Seeders;

/// <summary>
/// Seeds the database with realistic financial data using Bogus.
/// Only runs in Development; idempotent (checks before inserting).
/// </summary>
public sealed class DatabaseSeeder(ApplicationDbContext context, ILogger<DatabaseSeeder> logger)
{
    // Deterministic seed for reproducible test data
    private static readonly Randomizer Rng = new(8675309);

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting database seed...");

        await context.Database.MigrateAsync(cancellationToken);

        if (await context.Users.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Database already seeded — skipping.");
            return;
        }

        var users = SeedUsers();
        await context.Users.AddRangeAsync(users, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var accounts = SeedAccounts(users);
        await context.Accounts.AddRangeAsync(accounts, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        SeedTransactions(accounts);
        await context.SaveChangesAsync(cancellationToken);

        var budgets = SeedBudgets(users);
        await context.Budgets.AddRangeAsync(budgets, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Database seeded successfully. Users: {UserCount}, Accounts: {AccountCount}",
            users.Count, accounts.Count);
    }

    private static List<User> SeedUsers()
    {
        // Demo user with predictable credentials
        // BCrypt hash of "Demo@Password1!" with work factor 12
        const string demoHash = "$2a$12$seed.demo.hash.placeholder.AAAAAAAAAAAAAAAAAAAAAAAA";
        var demoUser = User.Create("demo@finsight.dev", demoHash, "Alex", "Chen");

        // Use a common hash for seed users (not for production use — seeder only)
        const string seedHash = "$2a$12$seed.user.hash.placeholder.AAAAAAAAAAAAAAAAAAAAAAAA";
        var faker = new Faker<User>()
            .CustomInstantiator(f => User.Create(
                f.Internet.Email(),
                seedHash,
                f.Name.FirstName(),
                f.Name.LastName()));

        var users = faker.Generate(4);
        users.Insert(0, demoUser);  // demo user always first

        // Clear domain events from seed (avoid event processing during seed)
        foreach (var u in users) u.ClearDomainEvents();

        return users;
    }

    private static List<Account> SeedAccounts(List<User> users)
    {
        var accounts = new List<Account>();

        foreach (var user in users)
        {
            // Every user gets a chequing + savings account
            var chequing = Account.Create(user.Id, "TD Chequing", AccountType.Checking, "CAD", "TD Bank");
            var savings = Account.Create(user.Id, "EQ Bank Savings", AccountType.Savings, "CAD", "EQ Bank");

            // Demo user also gets a credit card
            if (user.Email == "demo@finsight.dev")
            {
                var visa = Account.Create(user.Id, "TD Visa Infinite", AccountType.CreditCard, "CAD", "TD Bank");
                accounts.Add(visa);
            }

            accounts.Add(chequing);
            accounts.Add(savings);

            foreach (var a in accounts.TakeLast(user.Email == "demo@finsight.dev" ? 3 : 2))
                a.ClearDomainEvents();
        }

        return accounts;
    }

    private static void SeedTransactions(List<Account> accounts)
    {
        var now = DateTimeOffset.UtcNow;
        var merchantFaker = new Faker();

        // Merchant pools by category
        var merchants = new Dictionary<TransactionCategory, string[]>
        {
            [TransactionCategory.GroceriesAndFood] = ["Loblaws", "Metro", "Sobeys", "Tim Hortons", "McDonald's", "Chipotle", "Instacart"],
            [TransactionCategory.Transportation] = ["Presto", "Uber", "Shell", "Esso", "Petro-Canada", "Lyft"],
            [TransactionCategory.Entertainment] = ["Netflix", "Spotify", "Steam", "Cineplex", "Xbox Game Pass"],
            [TransactionCategory.ShoppingAndClothing] = ["Amazon", "IKEA", "Best Buy", "Walmart", "H&M", "Indigo"],
            [TransactionCategory.Utilities] = ["Toronto Hydro", "Enbridge Gas", "Bell Canada", "Rogers"],
            [TransactionCategory.HealthAndMedical] = ["Shoppers Drug Mart", "Rexall", "Ontario Health"],
        };

        foreach (var account in accounts)
        {
            if (account.Type == AccountType.Savings) continue;  // savings = fewer transactions

            // 3 months of realistic transaction history
            for (int monthsBack = 0; monthsBack < 3; monthsBack++)
            {
                var monthStart = now.AddMonths(-monthsBack).AddDays(-now.Day + 1);

                // Monthly salary credit
                if (account.Type == AccountType.Checking)
                {
                    var salary = new Money(Rng.Decimal(3500, 7000), "CAD");
                    account.RecordTransaction(
                        "Direct Deposit - Employer",
                        salary, TransactionType.Credit,
                        monthStart.AddDays(Rng.Int(1, 3)),
                        TransactionCategory.Salary);
                }

                // 15-30 spending transactions per month
                var txCount = Rng.Int(15, 30);
                for (int i = 0; i < txCount; i++)
                {
                    var category = Rng.ArrayElement([
                        TransactionCategory.GroceriesAndFood, TransactionCategory.GroceriesAndFood,  // weighted
                        TransactionCategory.Transportation,
                        TransactionCategory.Entertainment,
                        TransactionCategory.ShoppingAndClothing,
                        TransactionCategory.Utilities,
                        TransactionCategory.HealthAndMedical,
                    ]);

                    var merchantList = merchants.GetValueOrDefault(category, ["Unknown Merchant"]);
                    var merchant = Rng.ArrayElement(merchantList);

                    var amount = category switch
                    {
                        TransactionCategory.GroceriesAndFood => new Money(Rng.Decimal(8, 180), "CAD"),
                        TransactionCategory.Transportation => new Money(Rng.Decimal(3, 120), "CAD"),
                        TransactionCategory.Entertainment => new Money(Rng.Decimal(10, 80), "CAD"),
                        TransactionCategory.ShoppingAndClothing => new Money(Rng.Decimal(20, 400), "CAD"),
                        TransactionCategory.Utilities => new Money(Rng.Decimal(60, 200), "CAD"),
                        TransactionCategory.HealthAndMedical => new Money(Rng.Decimal(15, 150), "CAD"),
                        _ => new Money(Rng.Decimal(10, 100), "CAD"),
                    };

                    var date = monthStart.AddDays(Rng.Int(0, 27));
                    account.RecordTransaction(
                        $"Purchase at {merchant}",
                        amount, TransactionType.Debit,
                        date, category, merchant);
                }

                // Inject 1-2 anomalies (unusually large transactions)
                if (Rng.Bool() && account.Type == AccountType.Checking)
                {
                    var anomalyAmount = new Money(Rng.Decimal(800, 3000), "CAD");
                    var tx = account.RecordTransaction(
                        "Large Purchase",
                        anomalyAmount, TransactionType.Debit,
                        monthStart.AddDays(Rng.Int(5, 25)),
                        TransactionCategory.ShoppingAndClothing);
                    tx.MarkAsAnomaly();
                }
            }

            account.ClearDomainEvents();
        }
    }

    private static List<Budget> SeedBudgets(List<User> users)
    {
        var now = DateTimeOffset.UtcNow;
        var budgets = new List<Budget>();

        var categories = new[]
        {
            (TransactionCategory.GroceriesAndFood, 600m),
            (TransactionCategory.Transportation, 250m),
            (TransactionCategory.Entertainment, 150m),
            (TransactionCategory.ShoppingAndClothing, 300m),
            (TransactionCategory.Utilities, 200m),
            (TransactionCategory.HealthAndMedical, 100m),
        };

        foreach (var user in users)
        {
            foreach (var (category, limit) in categories)
            {
                var budget = Budget.Create(
                    user.Id, category,
                    new Money(limit, "CAD"),
                    now.Month, now.Year);

                // Simulate partial spending
                var spent = new Money(limit * Rng.Decimal(0.3m, 1.1m), "CAD");
                budget.AddSpending(spent);
                budgets.Add(budget);
            }
        }

        return budgets;
    }
}
