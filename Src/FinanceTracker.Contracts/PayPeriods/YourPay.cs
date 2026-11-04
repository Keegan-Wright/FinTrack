using System.ComponentModel;

namespace FinanceTracker.Contracts.PayPeriods;

/// <summary>
/// The user's pay, picked up from their transactions, and the period it puts them in.
/// </summary>
public class YourPay
{
    /// <summary>
    /// The income taken as the pay, null until one has come in like pay three times in three months.
    /// </summary>
    [Description("The income taken as the pay, null until one is found")]
    public Income? Pay { get; init; }

    /// <summary>
    /// Whether the user chose <see cref="Pay"/> themselves rather than it being the largest found.
    /// </summary>
    [Description("Whether the user chose the pay")]
    public bool IsChosen { get; init; }

    /// <summary>
    /// Whether the user turned pay periods off, back to calendar months.
    /// </summary>
    [Description("Whether pay periods are turned off")]
    public bool IsOff { get; init; }

    /// <summary>
    /// Whether periods run from pay to pay: there's a pay and they aren't turned off.
    /// </summary>
    [Description("Whether periods run from pay to pay")]
    public bool FollowsPay { get; init; }

    /// <summary>
    /// The period today is in.
    /// </summary>
    [Description("The period today is in")]
    public required PayPeriod CurrentPeriod { get; init; }
}
