using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Domain.Debts;

/// <summary>
/// Money the user owes and is paying off, e.g. a car loan, owed from <see cref="StartingBalance"/> on
/// <see cref="StartedOn"/> with interest at <see cref="InterestRate"/>.
/// </summary>
/// <remarks>
/// Which transactions paid it is kept on each <see cref="BankTransaction.DebtPayment"/>, and what's owed is never
/// stored, <see cref="DebtCalculator"/> works it out. A debt linked to an account, a loan or card that syncs its own
/// balance, owes what that balance says.
/// </remarks>
public sealed class Debt : AggregateRoot
{
    /// <summary>
    /// The highest <see cref="InterestRate"/> taken, as a percentage.
    /// </summary>
    public const decimal MaximumInterestRate = 100;

    [Obsolete("EF Use Only")]
#pragma warning disable CS8618 // EF Core sets the properties when it materialises the entity
    private Debt()
    {
    }
#pragma warning restore CS8618

    /// <param name="lender">Who it's owed to, null when the user doesn't say.</param>
    /// <param name="startingBalance">What was owed on <paramref name="startedOn"/>.</param>
    /// <param name="interestRate">The yearly rate (APR) as a percentage, 0 for none.</param>
    /// <exception cref="ArgumentException">The name is blank, the starting balance or monthly payment isn't more than
    /// 0, or the rate is outside 0 to <see cref="MaximumInterestRate"/>.</exception>
    public Debt(Guid userId, string name, string? lender, decimal startingBalance, DateOnly startedOn,
        decimal interestRate, decimal monthlyPayment) : base(userId)
    {
        Rename(name);
        ChangeLender(lender);
        ChangeStartingBalance(startingBalance, startedOn);
        ChangeInterestRate(interestRate);
        ChangeMonthlyPayment(monthlyPayment);
    }

    public string Name { get; private set; }

    /// <summary>
    /// Who it's owed to, null when the user doesn't say.
    /// </summary>
    public string? Lender { get; private set; }

    /// <summary>
    /// What was owed on <see cref="StartedOn"/>, before any payment linked to it.
    /// </summary>
    public decimal StartingBalance { get; private set; }

    /// <summary>
    /// The day <see cref="StartingBalance"/> was owed. Interest is added monthly from then, and payments count from
    /// then on.
    /// </summary>
    public DateOnly StartedOn { get; private set; }

    /// <summary>
    /// The yearly rate (APR) as a percentage, 0 for none. A twelfth of it is added each month.
    /// </summary>
    public decimal InterestRate { get; private set; }

    /// <summary>
    /// What the user pays each month, what the pay-off date is forecast from.
    /// </summary>
    public decimal MonthlyPayment { get; private set; }

    /// <summary>
    /// The loan or card account whose balance says what's owed, null when worked out from the payments.
    /// </summary>
    public Guid? AccountId { get; private set; }

    /// <summary>
    /// When the user wants it paid off by, null when they have no goal.
    /// </summary>
    public DateOnly? PayOffGoal { get; private set; }

    [MemberNotNull(nameof(Name))]
    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void ChangeLender(string? lender) => Lender = string.IsNullOrWhiteSpace(lender) ? null : lender.Trim();

    /// <exception cref="ArgumentOutOfRangeException">The balance isn't more than 0.</exception>
    public void ChangeStartingBalance(decimal startingBalance, DateOnly startedOn)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(startingBalance);
        StartingBalance = startingBalance;
        StartedOn = startedOn;
    }

    /// <exception cref="ArgumentOutOfRangeException">The rate is outside 0 to <see cref="MaximumInterestRate"/>.
    /// </exception>
    public void ChangeInterestRate(decimal interestRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(interestRate);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(interestRate, MaximumInterestRate);
        InterestRate = interestRate;
    }

    /// <exception cref="ArgumentOutOfRangeException">The payment isn't more than 0.</exception>
    public void ChangeMonthlyPayment(decimal monthlyPayment)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(monthlyPayment);
        MonthlyPayment = monthlyPayment;
    }

    /// <summary>
    /// Says what's owed comes from <paramref name="account"/>'s balance.
    /// </summary>
    /// <exception cref="InvalidOperationException">The account belongs to another user.</exception>
    public void LinkAccount(BankAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (account.UserId != UserId)
        {
            throw new InvalidOperationException("A debt can only follow the user's own accounts.");
        }

        AccountId = account.Id;
    }

    /// <summary>
    /// Works out what's owed from the payments again, e.g. when the account is gone.
    /// </summary>
    public void UnlinkAccount() => AccountId = null;

    public void SetPayOffGoal(DateOnly? payOffGoal) => PayOffGoal = payOffGoal;
}
