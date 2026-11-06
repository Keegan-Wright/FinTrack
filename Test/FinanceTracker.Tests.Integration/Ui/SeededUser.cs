using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Domain.Calendar;
using FinanceTracker.Domain.Debts;
using FinanceTracker.Domain.Household;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Domain.Rules;
using FinanceTracker.Domain.SavingsGoals;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// Something of everything the app shows for one user, so every page and dialog is seen with data in it rather
/// than only empty: two accounts with balances, this month's transactions (pay, spending, a tagged one, a pending
/// one, one paying the bill, a matched transfer, one to review, a repeat payment suggested as a subscription), and a
/// tag, budget, bill, debt, a savings goal typed in and one on the savings account, rule, household member and
/// calendar event. The names are what the tests look for.
/// </summary>
public static class SeededUser
{
    public const string Current = "Everyday account";
    public const string Savings = "Rainy day saver";

    public const string Pay = "Monthly pay";
    public const string Groceries = "Corner shop";
    public const string Pending = "Coffee house";
    public const string ToSavings = "Move to saver";
    public const string FromCurrent = "From everyday";
    public const string ToReview = "Payment to check";
    public const string Phone = "Mobile network";
    public const string GymPayment = "Gym direct debit";

    public const string Tag = "Food shop";
    public const string Budget = "Weekly shop";
    public const string Bill = "Gym membership";
    public const string Debt = "Car loan";
    public const string Goal = "Holiday fund";
    public const string AccountGoal = "House deposit";
    public const string Rule = "Coffee is not a transfer";
    public const string MemberFirstName = "Sam";
    public const string MemberLastName = "Example";
    public const string Event = "Car service";

    /// <summary>
    /// How many transactions are made up on top of the named ones, enough for the phone's list to show more and the
    /// grid to page. Each is older than the named ones, so those are always on the first page.
    /// </summary>
    public const int ExtraTransactions = 60;

    /// <summary>
    /// Gives <paramref name="username"/>, already in <paramref name="scenario"/>, everything above. Keyed in the
    /// scenario's mappings by the names here, e.g. <c>DebtMappings[Debt]</c>. Dated around <paramref name="now"/>, in
    /// UTC, the user's now.
    /// </summary>
    public static async Task SeedAsync(TestingScenario scenario, string username, DateTime now)
    {
        // Today where the app runs, as it reads it, e.g. 1 Jul when it's 23:30 on 30 Jun in UTC on summer time
        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, TimeZoneInfo.Local));

        await scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", username))
            // Consent well within its 90 days on the user's day, so nothing needs renewing
            .Do(s => s.BankConnectionScenario.WithConsentExpiresAt("bank", now.AddDays(80)))
            .Do(s => s.BankAccountScenario.WithAccount(Current, "bank", (userId, connectionId) =>
                new BankAccount(userId, connectionId, $"acc-{Guid.NewGuid():N}", "TRANSACTION", Current, "GBP")))
            .Do(s => s.BankAccountScenario.WithAccount(Savings, "bank", (userId, connectionId) =>
                new BankAccount(userId, connectionId, $"acc-{Guid.NewGuid():N}", "SAVINGS", Savings, "GBP")))
            .Do(s => s.BankAccountScenario.WithBalance(Current, new AccountBalance(1850.25m, 1902.65m, now)))
            .Do(s => s.BankAccountScenario.WithBalance(Savings, new AccountBalance(4200m, 4200m, now)))
            .Do(s => s.TransactionScenario.WithTransaction(Pay, Current, Details(Pay, 2400m, now.AddDays(-2))))
            .Do(s => s.TransactionScenario.WithTransaction(Groceries, Current,
                Details(Groceries, -42.18m, now.AddDays(-1))))
            .Do(s => s.TransactionScenario.WithTransaction(Pending, Current,
                Details(Pending, -3.20m, now.AddHours(-2)) with { IsPending = true }))
            .Do(s => s.TransactionScenario.WithTransaction(ToSavings, Current,
                Details(ToSavings, -200m, now.AddDays(-1))))
            .Do(s => s.TransactionScenario.WithTransaction(FromCurrent, Savings,
                Details(FromCurrent, 200m, now.AddDays(-1))))
            .Do(s => s.TransactionScenario.WithTransaction(ToReview, Current,
                Details(ToReview, -75m, now.AddDays(-3))))
            .Do(s => s.TransactionScenario.WithTransfer(ToSavings))
            .Do(s => s.TransactionScenario.WithTransfer(FromCurrent))
            .Do(s => s.TransactionScenario.WithTransfer(ToReview, TransferDecision.NeedsReview))
            .Do(s => s.TransactionScenario.WithTransactions(Current, Older(now)))
            .Do(s => s.ClassificationScenario.WithClassification(Tag, username, Tag))
            .Do(s => s.TransactionScenario.WithCustomClassification(Groceries, Tag));

        // Three months of the same phone payment, which is what a subscription suggestion is made from
        string[] phonePayments = ["phone-1", "phone-2", "phone-3"];
        for (int month = 0; month < phonePayments.Length; month++)
        {
            string payment = phonePayments[month];
            DateTime paidAt = now.AddDays(-4).AddMonths(month - 2);
            await scenario.Do(s => s.TransactionScenario.WithTransaction(payment, Current,
                Details(Phone, -24.99m, paidAt)));
        }

        await scenario.Do(s => s.BillScenario.WithSuggestion(Phone, username, Current, Phone, 24.99m,
                DateOnly.FromDateTime(now.AddDays(-4).AddMonths(-2)), DateOnly.FromDateTime(now.AddDays(-4)),
                phonePayments))
            .Do(s => s.BudgetScenario.WithBudget(Budget, username, userId => new Budget(userId, Budget,
                [scenario.ClassificationMappings[Tag]], 300m, BudgetMonth.Of(today), RolloverMode.None)))
            // First due today, so it's in this month whatever the day, and already paid
            .Do(s => s.BillScenario.WithBill(Bill, username, Current, (userId, accountId) => new Bill(userId,
                accountId, Bill, "Gym", BillFrequency.Monthly, 30m, today, BillSource.Manual)))
            .Do(s => s.TransactionScenario.WithTransaction(GymPayment, Current,
                Details(GymPayment, -30m, now.AddDays(-3))))
            .Do(s => s.TransactionScenario.WithBillPayment(GymPayment, Bill))
            .Do(s => s.DebtScenario.WithDebt(Debt, username, userId => new Debt(userId, Debt, "Example Finance",
                8000m, today.AddYears(-1), 6.9m, 250m)))
            .Do(s => s.SavingsGoalScenario.WithSavingsGoal(Goal, username, userId => new SavingsGoal(userId, Goal,
                450m, 100m, 1500m, now.AddMonths(10))))
            .Do(s => s.SavingsGoalScenario.WithSavingsGoal(AccountGoal, username, userId =>
                SavingsGoalOn(userId, scenario.AccountMappings[Savings], now)))
            .Do(s => s.RuleScenario.WithRule(Rule, username, userId => new Rule(userId, Rule, 0, RuleMatchMode.All,
                [new RuleCondition(RuleField.Description, RuleOperator.Contains, Pending)],
                [RuleAction.MarkNotATransfer()])))
            .Do(s => s.HouseholdMemberScenario.WithHouseholdMember(MemberFirstName, username, userId =>
                new HouseholdMember(userId, MemberFirstName, MemberLastName, 1200m)))
            .Do(s => s.CalendarEventScenario.WithEvent(Event, username, userId =>
                new CalendarEvent(userId, Event, today, EventRecurrence.None)));
    }

    // Saved as the savings account's balance, which a goal has a history of
    private static SavingsGoal SavingsGoalOn(Guid userId, Guid accountId, DateTime now)
    {
        SavingsGoal goal = new(userId, AccountGoal, 0m, 300m, 20000m, now.AddYears(3));
        goal.TrackAccount(accountId);
        return goal;
    }

    // Made up, one a day from five days back
    private static IEnumerable<TransactionDetails> Older(DateTime now) =>
        new TransactionDetailsFaker().Generate(ExtraTransactions)
            .Select((details, index) => details with { OccurredAt = now.AddDays(-5 - index), IsPending = false });

    private static TransactionDetails Details(string description, decimal amount, DateTime occurredAt)
    {
        TransactionType type = amount < 0 ? TransactionType.Debit : TransactionType.Credit;
        string category = amount < 0 ? "PURCHASE" : "CREDIT";
        return new TransactionDetails($"tx-{Guid.NewGuid():N}", description, type, category, amount, "GBP",
            occurredAt, false, [], TransactionMetadata.None, false);
    }
}
