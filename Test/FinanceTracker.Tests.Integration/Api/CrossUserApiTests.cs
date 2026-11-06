using System.Text.Json;
using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Integration.Ui;
using FinanceTracker.Tests.Unit.Shared.Scenarios;

namespace FinanceTracker.Tests.Integration.Api;

/// <summary>
/// Someone signed in can't reach another user's data through the API by its id: each call by id to another user's
/// transactions, bills, debts, goals, rules, tags, people, events and bank connections is refused, and leaves what
/// that user has as it was. The owner has something of everything, <see cref="SeededUser"/>.
/// </summary>
public class CrossUserApiTests : IntegrationTestBase
{
    /// <summary>
    /// Each call someone else makes with the owner's ids, by name.
    /// </summary>
    private static readonly Dictionary<string, Func<ApiSession, TestingScenario, CancellationToken, Task>> Attempts =
        new()
        {
            ["Split a transaction"] = static (api, ids, ct) => api.Get<ITransactionsService>()
                .SplitAsync(Groceries(ids), new SplitTransaction
                {
                    Parts = [new SplitTransactionPart { Amount = -40m }, new SplitTransactionPart { Amount = -2.18m }]
                }, ct),
            ["Remove a transaction's split"] = static (api, ids, ct) => api.Get<ITransactionsService>()
                .UnsplitAsync(Groceries(ids), ct),
            ["Hide a bank classification"] = static (api, ids, ct) => api.Get<ITransactionsService>()
                .HideBankClassificationAsync(Groceries(ids), new ChangeBankClassification
                {
                    Classification = "Groceries"
                }, ct),
            ["Show a bank classification"] = static (api, ids, ct) => api.Get<ITransactionsService>()
                .ShowBankClassificationAsync(Groceries(ids), new ChangeBankClassification
                {
                    Classification = "Groceries"
                }, ct),
            ["Mark a transfer"] = static (api, ids, ct) => api.Get<ITransfersService>()
                .MarkAsTransferAsync(ids.TransactionMappings[SeededUser.ToReview], ct),
            ["Mark not a transfer"] = static (api, ids, ct) => api.Get<ITransfersService>()
                .MarkNotATransferAsync(ids.TransactionMappings[SeededUser.ToSavings], ct),
            ["Reset a transfer"] = static (api, ids, ct) => api.Get<ITransfersService>()
                .ResetTransferAsync(ids.TransactionMappings[SeededUser.ToSavings], ct),
            ["Tag a transaction"] = static (api, ids, ct) => api.Get<IClassificationService>()
                .AddCustomClassificationsToTransactionAsync(new AddCustomClassificationsToTransaction
                {
                    TransactionId = ids.TransactionMappings[SeededUser.Pay],
                    Classifications = [new SelectedCustomClassification { ClassificationId = Tag(ids) }]
                }, ct),
            ["Rename a tag"] = static (api, ids, ct) => api.Get<IClassificationService>()
                .RenameCustomClassificationAsync(Tag(ids), new RenameClassification { Tag = "Taken" }, ct),
            ["Delete a tag"] = static (api, ids, ct) => api.Get<IClassificationService>()
                .RemoveCustomClassificationAsync(Tag(ids), ct),
            ["Get a bill"] = static (api, ids, ct) => api.Get<IBillsService>().GetBillAsync(Bill(ids), ct),
            ["End a bill"] = static (api, ids, ct) => api.Get<IBillsService>().EndBillAsync(Bill(ids), ct),
            ["Reopen a bill"] = static (api, ids, ct) => api.Get<IBillsService>().ReopenBillAsync(Bill(ids), ct),
            ["Delete a bill"] = static (api, ids, ct) => api.Get<IBillsService>().DeleteBillAsync(Bill(ids), ct),
            ["Pay a bill"] = static (api, ids, ct) => api.Get<IBillsService>()
                .PayBillAsync(Groceries(ids), new PayBill { BillId = Bill(ids) }, ct),
            ["Confirm a subscription"] = static (api, ids, ct) => api.Get<IBillsService>()
                .ConfirmSuggestionAsync(ids.BillSuggestionMappings[SeededUser.Phone], ct),
            ["Dismiss a subscription"] = static (api, ids, ct) => api.Get<IBillsService>()
                .DismissSuggestionAsync(ids.BillSuggestionMappings[SeededUser.Phone], ct),
            ["Get a debt"] = static (api, ids, ct) => api.Get<IDebtsService>().GetDebtAsync(Debt(ids), ct),
            ["Forecast a debt"] = static (api, ids, ct) => api.Get<IDebtsService>()
                .ForecastAsync(Debt(ids), 300m, ct),
            ["Delete a debt"] = static (api, ids, ct) => api.Get<IDebtsService>().DeleteDebtAsync(Debt(ids), ct),
            ["Pay off a debt"] = static (api, ids, ct) => api.Get<IDebtsService>()
                .PayDebtAsync(Groceries(ids), new PayDebt { DebtId = Debt(ids) }, ct),
            ["Get a savings goal"] = static (api, ids, ct) => api.Get<ISavingsGoalsService>()
                .GetSavingsGoalAsync(ids.SavingsGoalMappings[SeededUser.Goal], ct),
            ["Get a savings goal's history"] = static (api, ids, ct) => api.Get<ISavingsGoalsService>()
                .GetSavingsGoalHistoryAsync(ids.SavingsGoalMappings[SeededUser.AccountGoal], ct),
            ["Delete a savings goal"] = static async (api, ids, ct) => RefusedUnlessDone(
                await api.Get<ISavingsGoalsService>().DeleteSavingsGoalAsync(ids.SavingsGoalMappings[SeededUser.Goal],
                    ct)),
            ["Get a rule"] = static (api, ids, ct) => api.Get<IRulesService>().GetRuleAsync(Rule(ids), ct),
            ["Apply a rule"] = static (api, ids, ct) => api.Get<IRulesService>()
                .ApplyRuleToExistingAsync(Rule(ids), ct),
            ["Delete a rule"] = static (api, ids, ct) => api.Get<IRulesService>().DeleteRuleAsync(Rule(ids), ct),
            ["Start a rule from a transaction"] = static (api, ids, ct) => api.Get<IRulesService>()
                .GetDraftFromTransactionAsync(Groceries(ids), ct),
            ["Get a budget"] = static (api, ids, ct) => api.Get<IBudgetsService>()
                .GetBudgetAsync(ids.BudgetMappings[SeededUser.Budget], ct),
            ["Delete a budget"] = static (api, ids, ct) => api.Get<IBudgetsService>()
                .DeleteBudgetAsync(ids.BudgetMappings[SeededUser.Budget], ct),
            ["Delete a person"] = static async (api, ids, ct) => RefusedUnlessDone(
                await api.Get<IHouseholdMemberService>()
                    .DeleteHouseholdMemberAsync(ids.HouseholdMemberMappings[SeededUser.MemberFirstName], ct)),
            ["Delete an event"] = static (api, ids, ct) => api.Get<ICalendarService>()
                .DeleteEventAsync(ids.CalendarEventMappings[SeededUser.Event], ct),
            ["Renew a bank connection"] = static (api, ids, ct) => api.Get<IOpenBankingService>()
                .GetRenewalUrlAsync(ids.ConnectionMappings["bank"], ct)
        };

    public static IEnumerable<string> AttemptNames() => Attempts.Keys;

    [Test]
    [MethodDataSource(nameof(AttemptNames))]
    public async Task AnotherUsersData_ById_IsRefusedAndLeftAsItWas(string attempt)
    {
        // Arrange
        SignedInUser owner = await RegisterUserAsync();
        await SeededUser.SeedAsync(Scenario, owner.Username, NowFor(owner));
        SignedInUser other = await RegisterUserAsync();
        string before = await SnapshotAsync(ApiFor(owner));

        // Act
        bool refused = await IsRefusedAsync(() => Attempts[attempt](ApiFor(other), Scenario, CancellationToken));

        // Assert
        string after = await SnapshotAsync(ApiFor(owner));
        using (Assert.Multiple())
        {
            await Assert.That(refused).IsTrue();
            await Assert.That(after).IsEqualTo(before);
        }
    }

    [Test]
    public async Task AnotherUsersData_IsInNoneOfTheirLists()
    {
        // Arrange
        SignedInUser owner = await RegisterUserAsync();
        await SeededUser.SeedAsync(Scenario, owner.Username, NowFor(owner));
        SignedInUser other = await RegisterUserAsync();

        // Act
        string theirs = await SnapshotAsync(ApiFor(other));

        // Assert, nothing of the owner's by name
        using (Assert.Multiple())
        {
            foreach (string name in OwnersNames)
            {
                await Assert.That(theirs).DoesNotContain(name);
            }
        }
    }

    private static readonly string[] OwnersNames =
    [
        SeededUser.Groceries, SeededUser.Current, SeededUser.Bill, SeededUser.Debt, SeededUser.Goal, SeededUser.Rule,
        SeededUser.Tag, SeededUser.Budget, SeededUser.MemberFirstName, SeededUser.Event, SeededUser.Phone
    ];

    private static Guid Groceries(TestingScenario ids) => ids.TransactionMappings[SeededUser.Groceries];

    private static Guid Tag(TestingScenario ids) => ids.ClassificationMappings[SeededUser.Tag];

    private static Guid Bill(TestingScenario ids) => ids.BillMappings[SeededUser.Bill];

    private static Guid Debt(TestingScenario ids) => ids.DebtMappings[SeededUser.Debt];

    private static Guid Rule(TestingScenario ids) => ids.RuleMappings[SeededUser.Rule];

    /// <summary>
    /// A delete that answers whether it deleted anything, as not found for someone else's, refuses with false.
    /// </summary>
    private static void RefusedUnlessDone(bool done)
    {
        if (!done)
        {
            throw new InvalidOperationException("Nothing of theirs was found");
        }
    }

    private static async Task<bool> IsRefusedAsync(Func<Task> attempt)
    {
        try
        {
            await attempt();
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    /// <summary>
    /// Everything the user has, as the API lists it, to compare before and after.
    /// </summary>
    private async Task<string> SnapshotAsync(ApiSession api)
    {
        Dictionary<string, object> lists = new()
        {
            ["transactions"] = await api.Get<ITransactionsService>()
                .GetAllTransactionsAsync(new FilterTransactions(), CancellationToken).ToListAsync(CancellationToken),
            ["accounts"] = await api.Get<ITransactionsService>()
                .GetAccountsForTransactionFiltersAsync(CancellationToken).ToListAsync(CancellationToken),
            ["tags"] = await api.Get<IClassificationService>()
                .GetAllCustomClassificationsAsync(CancellationToken).ToListAsync(CancellationToken),
            ["bills"] = await api.Get<IBillsService>().GetBillsAsync(CancellationToken).ToListAsync(CancellationToken),
            ["subscriptions"] = await api.Get<IBillsService>()
                .GetSuggestionsAsync(CancellationToken).ToListAsync(CancellationToken),
            ["debts"] = await api.Get<IDebtsService>().GetDebtsAsync(CancellationToken).ToListAsync(CancellationToken),
            ["goals"] = await api.Get<ISavingsGoalsService>()
                .GetSavingsGoalsAsync(CancellationToken).ToListAsync(CancellationToken),
            ["rules"] = await api.Get<IRulesService>().GetRulesAsync(CancellationToken).ToListAsync(CancellationToken),
            ["budgets"] = await api.Get<IBudgetsService>()
                .GetBudgetsAsync(CancellationToken).ToListAsync(CancellationToken),
            ["people"] = await api.Get<IHouseholdMemberService>()
                .GetHouseholdMembersAsync(CancellationToken).ToListAsync(CancellationToken),
            ["events"] = await api.Get<ICalendarService>()
                .GetEventsAsync(CancellationToken).ToListAsync(CancellationToken),
            ["connections"] = await api.Get<IOpenBankingService>()
                .GetConnectionsAsync(CancellationToken).ToListAsync(CancellationToken)
        };

        return JsonSerializer.Serialize(lists);
    }
}
