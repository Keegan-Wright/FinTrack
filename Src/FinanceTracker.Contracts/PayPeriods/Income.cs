using System.ComponentModel;

namespace FinanceTracker.Contracts.PayPeriods;

/// <summary>
/// Money that comes in like pay, about monthly from the same payer into the same account.
/// </summary>
public class Income
{
    /// <summary>
    /// The account it's paid into.
    /// </summary>
    [Description("The account it's paid into")]
    public Guid AccountId { get; init; }

    /// <summary>
    /// The account's name.
    /// </summary>
    [Description("The account's name")]
    public required string AccountName { get; init; }

    /// <summary>
    /// The payer in upper case letters only, which with <see cref="AccountId"/> says which payments it is. Sent back
    /// to choose it, see <see cref="ChooseIncome"/>.
    /// </summary>
    [Description("The payer in upper case letters only, to choose it")]
    public required string Key { get; init; }

    /// <summary>
    /// The payer as the bank names them.
    /// </summary>
    [Description("The payer as the bank names them")]
    public required string Name { get; init; }

    /// <summary>
    /// The latest pay.
    /// </summary>
    [Description("The latest pay")]
    public decimal Amount { get; init; }

    /// <summary>
    /// Which day of the month it comes in.
    /// </summary>
    [Description("Which day of the month it comes in")]
    public PayDayKind Kind { get; init; }

    /// <summary>
    /// The day of the month for <see cref="PayDayKind.DayOfMonth"/>, 0 otherwise.
    /// </summary>
    [Description("The day of the month it comes in, 0 for the last working day")]
    public int Day { get; init; }

    /// <summary>
    /// The day the latest came in where the user is.
    /// </summary>
    [Description("When the latest came in")]
    public DateOnly LastPaidOn { get; init; }
}
