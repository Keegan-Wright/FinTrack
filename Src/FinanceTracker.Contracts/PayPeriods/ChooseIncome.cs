using System.ComponentModel;

namespace FinanceTracker.Contracts.PayPeriods;

/// <summary>
/// Which of the user's incomes is their pay, see <see cref="Income"/>.
/// </summary>
public class ChooseIncome
{
    /// <summary>
    /// The account it's paid into, see <see cref="Income.AccountId"/>.
    /// </summary>
    [Description("The account it's paid into")]
    public Guid AccountId { get; init; }

    /// <summary>
    /// The payer as <see cref="Income.Key"/> gave it.
    /// </summary>
    [Description("The payer in upper case letters only, as the income gave it")]
    public required string Key { get; init; }
}
