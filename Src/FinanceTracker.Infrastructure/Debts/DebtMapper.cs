using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Debts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using Debt = FinanceTracker.Contracts.Debts.Debt;
using DebtForecast = FinanceTracker.Contracts.Debts.DebtForecast;
using DomainDebt = FinanceTracker.Domain.Debts.Debt;
using DomainDebtForecast = FinanceTracker.Domain.Debts.DebtForecast;

namespace FinanceTracker.Infrastructure.Debts;

internal static class DebtMapper
{
    /// <param name="accountName">The linked account's name, null when it isn't linked.</param>
    /// <param name="sharedBy">Whoever shares it with the user, null when it's theirs.</param>
    /// <param name="canChange">Whether the user can edit it.</param>
    public static Debt ToContract(DomainDebt debt, string? accountName, DebtProgress progress,
        string? sharedBy = null, bool canChange = true) => new()
    {
        Id = debt.Id,
        Name = debt.Name,
        Lender = debt.Lender,
        StartingBalance = debt.StartingBalance,
        StartedOn = debt.StartedOn,
        InterestRate = debt.InterestRate,
        MonthlyPayment = debt.MonthlyPayment,
        AccountId = debt.AccountId,
        AccountName = accountName,
        PayOffGoal = debt.PayOffGoal,
        Owed = progress.Owed,
        Paid = progress.Paid,
        PaidOffPercent = progress.PaidOffPercent,
        Forecast = ToContract(progress.Forecast),
        OnTrack = progress.OnTrack,
        SharedBy = sharedBy,
        CanChange = canChange
    };

    public static DebtForecast ToContract(DomainDebtForecast forecast) => new()
    {
        PaidOffOn = forecast.PaidOffOn,
        Months = forecast.Months,
        Interest = forecast.Interest
    };

    public static DebtBalancePoint ToContract(DebtBalance balance) => new()
    {
        On = balance.On,
        Owed = balance.Owed
    };

    /// <param name="payment">A transaction paying off the debt.</param>
    public static DebtPaymentItem ToItem(BankTransaction payment, string accountName, string? ruleName) => new()
    {
        TransactionId = payment.Id,
        Description = payment.Description,
        AccountName = accountName,
        Amount = -payment.Amount,
        PaidAt = payment.OccurredAt,
        DecidedBy = DecidedBy(payment.DebtPayment!.Source),
        RuleName = ruleName
    };

    public static TransferDecidedBy DecidedBy(DecisionSource source)
    {
        if (source == DecisionSource.Rule)
        {
            return TransferDecidedBy.Rule;
        }

        return TransferDecidedBy.User;
    }
}
