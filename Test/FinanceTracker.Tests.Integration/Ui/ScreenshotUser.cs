using Bogus;
using System.Text;
using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.Classifications;
using FinanceTracker.Domain.NetWorth;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Balances;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.Rules;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// Six months of an everyday life for the README's screenshots, see <see cref="ScreenshotTests"/>. Two banks with two
/// accounts each, pay, rent, bills, shopping and a monthly move to savings, a tag and a rule, with each day's balances
/// so net worth draws a line. Every name and amount is made up, and the same every run.
/// </summary>
public static class ScreenshotUser
{
    /// <summary>
    /// The name the dashboard greets them by, made up by Bogus. Seeded, so it's the same every run.
    /// </summary>
    public static string FirstName { get; } = new Faker("en_GB") { Random = new Randomizer(17) }.Name.FirstName();

    /// <summary>
    /// The partner they share with, made up by Bogus like <see cref="FirstName"/>.
    /// </summary>
    public static Person Partner { get; } = new Faker("en_GB") { Random = new Randomizer(29) }.Person;

    /// <summary>
    /// The rule, by its scenario name in <see cref="TestingScenario.RuleMappings"/>.
    /// </summary>
    public const string RuleName = "Coffee is eating out";

    /// <summary>
    /// The account shared with a partner, by its scenario name in <see cref="TestingScenario.AccountMappings"/>.
    /// </summary>
    public const string BillsAccount = "Bills account";

    private const string Current = "Everyday account";
    private const string Savings = "Rainy day saver";
    private const string HolidaySaver = "Holiday saver";
    private const string FirstBank = "Northgate Bank";
    private const string SecondBank = "Harbour Building Society";
    private const decimal BillsAccountBalance = 640.10m;
    private const decimal HolidaySaverBalance = 1275.50m;
    private const int Days = 180;
    private const decimal CurrentBalance = 1850.25m;
    private const decimal SavingsBalance = 4200m;
    private const decimal ToSavingsEachMonth = 300m;
    private const string ToSavings = "Move to saver";
    private const string FromCurrent = "From everyday";

    /// <summary>
    /// The bills, each paid monthly on its day.
    /// </summary>
    private static readonly (string Name, decimal Amount, int Day, string Category)[] Bills =
    [
        ("Energy supplier", 86.40m, 5, "Bills"),
        ("Broadband", 32m, 10, "Bills"),
        ("Streaming service", 10.99m, 12, "Entertainment"),
        ("Gym membership", 30m, 15, "Health")
    ];

    /// <summary>
    /// Gives <paramref name="username"/>, already in <paramref name="scenario"/>, the six months up to
    /// <paramref name="now"/>, in UTC.
    /// </summary>
    /// <param name="scenario">The scenario the user is in.</param>
    /// <param name="username">The user.</param>
    /// <param name="now">The user's now, in UTC.</param>
    /// <param name="dbContextFactory">Saves the balance history, which has no scenario of its own.</param>
    public static async Task SeedAsync(TestingScenario scenario, string username, DateTime now,
        IDbContextFactory<FinanceTrackerContext> dbContextFactory)
    {
        DateOnly today = DateOnly.FromDateTime(now);
        DateOnly start = today.AddDays(-Days);

        await scenario.Do(s => s.BankConnectionScenario.WithConnection(FirstBank, username, userId =>
                Connection(userId, FirstBank, "N", "#1f6f5c", now)))
            .Do(s => s.BankConnectionScenario.WithConnection(SecondBank, username, userId =>
                Connection(userId, SecondBank, "H", "#2b4c8c", now)))
            .Do(s => s.BankAccountScenario.WithAccount(Current, FirstBank, (userId, connectionId) =>
                new BankAccount(userId, connectionId, $"acc-{Guid.NewGuid():N}", "TRANSACTION", Current, "GBP")))
            .Do(s => s.BankAccountScenario.WithAccount(Savings, FirstBank, (userId, connectionId) =>
                new BankAccount(userId, connectionId, $"acc-{Guid.NewGuid():N}", "SAVINGS", Savings, "GBP")))
            .Do(s => s.BankAccountScenario.WithAccount(BillsAccount, SecondBank, (userId, connectionId) =>
                new BankAccount(userId, connectionId, $"acc-{Guid.NewGuid():N}", "TRANSACTION", BillsAccount, "GBP")))
            .Do(s => s.BankAccountScenario.WithAccount(HolidaySaver, SecondBank, (userId, connectionId) =>
                new BankAccount(userId, connectionId, $"acc-{Guid.NewGuid():N}", "SAVINGS", HolidaySaver, "GBP")))
            .Do(s => s.BankAccountScenario.WithBalance(Current, Balance(CurrentBalance, now)))
            .Do(s => s.BankAccountScenario.WithBalance(Savings, Balance(SavingsBalance, now)))
            .Do(s => s.BankAccountScenario.WithBalance(BillsAccount, Balance(BillsAccountBalance, now)))
            .Do(s => s.BankAccountScenario.WithBalance(HolidaySaver, Balance(HolidaySaverBalance, now)));

        // The same every run, so the screenshots only change when the app does
        Random random = new(17);
        List<TransactionDetails> currentSpending = [];
        List<TransactionDetails> savingsIncome = [];
        for (DateOnly day = start; day < today; day = day.AddDays(1))
        {
            AddEverydayTransactions(day, random, currentSpending);
            if (day.Day == 1)
            {
                savingsIncome.Add(Details("Interest", Money(random, 6m, 9m), day, "Income"));
            }
        }

        await scenario.Do(s => s.TransactionScenario.WithTransactions(Current, currentSpending))
            .Do(s => s.TransactionScenario.WithTransactions(Savings, savingsIncome));

        List<TransactionDetails> named = [];
        named.AddRange(await AddBillsAsync(scenario, username, start, today));
        named.AddRange(await AddMovesToSavingsAsync(scenario, start, today));

        Dictionary<string, List<TransactionDetails>> byAccount = new()
        {
            [Current] = [.. currentSpending, .. named.Where(static x => x.Description != FromCurrent)],
            [Savings] = [.. savingsIncome, .. named.Where(static x => x.Description == FromCurrent)],
            [BillsAccount] = [],
            [HolidaySaver] = []
        };
        await AddBalanceHistoryAsync(scenario, username, start, today, now, byAccount, dbContextFactory);
        await AddCoffeeRuleAsync(scenario, username, dbContextFactory);
    }

    /// <summary>
    /// A connection to <paramref name="name"/>, with a made up logo of its initial on a coloured tile.
    /// </summary>
    private static BankConnection Connection(Guid userId, string name, string initial, string colour, DateTime now)
    {
        string svg = $"""
                      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 40 40">
                        <rect width="40" height="40" rx="8" fill="{colour}"/>
                        <text x="20" y="27" font-family="sans-serif" font-size="20" font-weight="bold" fill="#fff" text-anchor="middle">{initial}</text>
                      </svg>
                      """;
        return new BankConnection(userId, $"ob-{Guid.NewGuid():N}", name, $"code-{Guid.NewGuid():N}",
            ["info", "accounts", "balance", "transactions", "offline_access"], Encoding.UTF8.GetBytes(svg),
            now.AddDays(-10), now.AddDays(80));
    }

    private static AccountBalance Balance(decimal balance, DateTime now) => new(balance, balance, now);

    /// <summary>
    /// A "Coffee" tag, and a rule adding it to coffee, for the rule editor's screenshot to preview.
    /// </summary>
    private static async Task AddCoffeeRuleAsync(TestingScenario scenario, string username,
        IDbContextFactory<FinanceTrackerContext> dbContextFactory)
    {
        CustomClassification coffee = new(scenario.UserMappings[username], "Coffee");
        await using (FinanceTrackerContext context = await dbContextFactory.CreateDbContextAsync())
        {
            context.CustomClassifications.Add(coffee);
            await context.SaveChangesAsync();
        }

        await scenario.Do(s => s.RuleScenario.WithRule(RuleName, username, userId => new Rule(userId, RuleName, 0,
            RuleMatchMode.All, [new RuleCondition(RuleField.Description, RuleOperator.Contains, "Coffee")],
            [RuleAction.AddTag(coffee)])));
    }

    /// <summary>
    /// A day's pay, rent, shopping, eating out and travel on the everyday account.
    /// </summary>
    /// <param name="day">The day.</param>
    /// <param name="random">Picks the amounts.</param>
    /// <param name="transactions">Where they're added.</param>
    private static void AddEverydayTransactions(DateOnly day, Random random, List<TransactionDetails> transactions)
    {
        // Paid on the 1st, so whatever day the screenshots are taken this month has its pay in it
        if (day.Day == 1)
        {
            transactions.Add(Details("Monthly pay", 2200m, day, "Income"));
        }

        if (day.Day == 3)
        {
            transactions.Add(Details("Home rent", -950m, day, "Bills"));
        }

        if (day.Day == 18)
        {
            transactions.Add(Details("Online shop", -Money(random, 20m, 80m), day, "Shopping"));
        }

        switch (day.DayOfWeek)
        {
            case DayOfWeek.Monday:
                transactions.Add(Details("Train tickets", -Money(random, 14m, 26m), day, "Transport"));
                transactions.Add(Details("Coffee house", -Money(random, 3.2m, 4.6m), day, "Eating out"));
                break;
            case DayOfWeek.Tuesday:
            case DayOfWeek.Thursday:
                transactions.Add(Details("Coffee house", -Money(random, 3.2m, 4.6m), day, "Eating out"));
                break;
            case DayOfWeek.Wednesday:
                transactions.Add(Details("Corner shop", -Money(random, 8m, 25m), day, "Groceries"));
                break;
            case DayOfWeek.Friday when day.DayOfYear % 14 < 7:
                transactions.Add(Details("Noodle bar", -Money(random, 24m, 58m), day, "Eating out"));
                break;
            case DayOfWeek.Saturday:
                transactions.Add(Details("Supermarket", -Money(random, 55m, 110m), day, "Groceries"));
                break;
        }
    }

    /// <summary>
    /// The bills, each with its monthly payments linked, so the bills page shows them paid.
    /// </summary>
    /// <returns>The payments.</returns>
    private static async Task<List<TransactionDetails>> AddBillsAsync(TestingScenario scenario, string username,
        DateOnly start, DateOnly today)
    {
        List<TransactionDetails> payments = [];
        foreach ((string name, decimal amount, int dueDay, string category) in Bills)
        {
            DateOnly firstDue = FirstOnDay(start, dueDay);
            await scenario.Do(s => s.BillScenario.WithBill(name, username, Current, (userId, accountId) =>
                new Bill(userId, accountId, name, name, BillFrequency.Monthly, amount, firstDue, BillSource.Manual)));

            for (DateOnly due = firstDue; due < today; due = due.AddMonths(1))
            {
                string payment = $"{name} {due:yyyy-MM}";
                TransactionDetails details = Details(name, -amount, due, category);
                payments.Add(details);
                await scenario.Do(s => s.TransactionScenario.WithTransaction(payment, Current, details))
                    .Do(s => s.TransactionScenario.WithBillPayment(payment, name));
            }
        }

        return payments;
    }

    /// <summary>
    /// A move from the everyday account to savings each month, matched as a transfer so it isn't spending.
    /// </summary>
    /// <returns>Both sides of each move.</returns>
    private static async Task<List<TransactionDetails>> AddMovesToSavingsAsync(TestingScenario scenario,
        DateOnly start, DateOnly today)
    {
        List<TransactionDetails> moves = [];
        for (DateOnly day = FirstOnDay(start, 2); day < today; day = day.AddMonths(1))
        {
            string outName = $"{ToSavings} {day:yyyy-MM}";
            string inName = $"{FromCurrent} {day:yyyy-MM}";
            // Labelled a transfer by the bank, as the matcher needs to be sure when sharing makes it run again
            TransactionDetails moveOut = Details(ToSavings, -ToSavingsEachMonth, day, "Transfer") with
            {
                LabelledAsTransfer = true
            };
            TransactionDetails moveIn = Details(FromCurrent, ToSavingsEachMonth, day, "Transfer") with
            {
                LabelledAsTransfer = true
            };
            moves.Add(moveOut);
            moves.Add(moveIn);

            await scenario.Do(s => s.TransactionScenario.WithTransaction(outName, Current, moveOut))
                .Do(s => s.TransactionScenario.WithTransaction(inName, Savings, moveIn))
                .Do(s => s.TransactionScenario.WithTransfer(outName))
                .Do(s => s.TransactionScenario.WithTransfer(inName));
        }

        return moves;
    }

    /// <summary>
    /// Each day's closing balances, worked back from today's through the transactions, and the net worth they add up
    /// to, as a sync and the backfill would leave them.
    /// </summary>
    private static async Task AddBalanceHistoryAsync(TestingScenario scenario, string username, DateOnly start,
        DateOnly today, DateTime now, Dictionary<string, List<TransactionDetails>> byAccount,
        IDbContextFactory<FinanceTrackerContext> dbContextFactory)
    {
        Guid userId = scenario.UserMappings[username];
        Dictionary<string, decimal> balances = new()
        {
            [Current] = CurrentBalance,
            [Savings] = SavingsBalance,
            [BillsAccount] = BillsAccountBalance,
            [HolidaySaver] = HolidaySaverBalance
        };

        await using FinanceTrackerContext context = await dbContextFactory.CreateDbContextAsync();
        for (DateOnly day = today; day >= start; day = day.AddDays(-1))
        {
            DateTime asOf = day == today ? now : day.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            List<BalanceSnapshot> snapshots = [];
            foreach ((string account, decimal balance) in balances)
            {
                snapshots.Add(new BalanceSnapshot(userId, scenario.AccountMappings[account], day,
                    new AccountBalance(balance, balance, asOf), BalanceSnapshotSource.Synced, isStale: false));
            }

            context.BalanceSnapshots.AddRange(snapshots);
            context.NetWorthSnapshots.Add(new NetWorthSnapshot(userId, day, snapshots));

            // The day before closed on this day's balance less what moved on this day
            foreach (string account in balances.Keys.ToList())
            {
                balances[account] -= byAccount[account]
                    .Where(x => DateOnly.FromDateTime(x.OccurredAt) == day)
                    .Sum(static x => x.Amount);
            }
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// The first <paramref name="dayOfMonth"/> on or after <paramref name="from"/>.
    /// </summary>
    private static DateOnly FirstOnDay(DateOnly from, int dayOfMonth)
    {
        DateOnly candidate = new(from.Year, from.Month, dayOfMonth);
        if (candidate < from)
        {
            return candidate.AddMonths(1);
        }

        return candidate;
    }

    /// <summary>
    /// An amount between <paramref name="min"/> and <paramref name="max"/>, to the penny.
    /// </summary>
    private static decimal Money(Random random, decimal min, decimal max) =>
        Math.Round(min + (max - min) * (decimal)random.NextDouble(), 2);

    /// <summary>
    /// A settled transaction at midday on <paramref name="day"/>, tagged by the bank as <paramref name="category"/>.
    /// </summary>
    private static TransactionDetails Details(string description, decimal amount, DateOnly day, string category)
    {
        TransactionType type = amount < 0 ? TransactionType.Debit : TransactionType.Credit;
        string providerCategory = amount < 0 ? "PURCHASE" : "CREDIT";
        return new TransactionDetails($"tx-{Guid.NewGuid():N}", description, type, providerCategory, amount, "GBP",
            day.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc), false, [category], TransactionMetadata.None,
            false);
    }
}
