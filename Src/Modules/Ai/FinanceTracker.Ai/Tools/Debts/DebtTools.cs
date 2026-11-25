using System.ComponentModel;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Services;

namespace FinanceTracker.Ai.Tools.Debts;

/// <summary>
/// Tools for the user's debts.
/// </summary>
/// <param name="debtsService">Reads the debts, the same as the debts page.</param>
/// <param name="pageLinks">Makes the links to the pages its results can be checked on.</param>
public sealed class DebtTools(IDebtsService debtsService, PageLinks pageLinks)
{
    /// <summary>
    /// The user's debts.
    /// </summary>
    /// <param name="cancellationToken">Stops the work.</param>
    [AiTool("get_debts")]
    [Description("Lists the user's debts, such as loans and credit cards: what's owed, the interest rate, the " +
        "monthly payment and whether each is on track to be paid off.")]
    public async Task<DebtList> GetDebtsAsync(CancellationToken cancellationToken = default)
    {
        List<DebtSummary> debts = [];
        await foreach (Debt debt in debtsService.GetDebtsAsync(cancellationToken))
        {
            debts.Add(new DebtSummary(debt.Name, debt.Lender, debt.Owed, debt.PaidOffPercent, debt.InterestRate,
                debt.MonthlyPayment, debt.PayOffGoal, debt.OnTrack));
        }

        return new DebtList(debts, debts.Sum(static x => x.Owed), pageLinks.For(ToolLinks.Debts));
    }
}
