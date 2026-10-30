using System.ComponentModel;

namespace FinanceTracker.Contracts.Bills;

/// <summary>
/// Repeat payments that look like a subscription, waiting for the user to confirm them as a bill or dismiss them.
/// </summary>
public class BillSuggestion
{
    /// <summary>
    /// Unique identifier for the suggestion.
    /// </summary>
    [Description("Unique identifier for the suggestion")]
    public Guid Id { get; init; }

    /// <summary>
    /// The payee as the bank names them, the bill's name if confirmed.
    /// </summary>
    [Description("The payee, the bill's name if confirmed")]
    public required string Name { get; init; }

    /// <summary>
    /// The account the payments come out of.
    /// </summary>
    [Description("The account the payments come out of")]
    public Guid AccountId { get; init; }

    /// <summary>
    /// The name of the account the payments come out of.
    /// </summary>
    [Description("The name of the account the payments come out of")]
    public required string AccountName { get; init; }

    /// <summary>
    /// How often the payments repeat.
    /// </summary>
    [Description("How often the payments repeat")]
    public BillFrequency Frequency { get; init; }

    /// <summary>
    /// The latest payment's amount.
    /// </summary>
    [Description("The latest payment's amount")]
    public decimal Amount { get; init; }

    /// <summary>
    /// When the next payment would be due, a period after the latest.
    /// </summary>
    [Description("When the next payment would be due")]
    public DateOnly? NextDue { get; init; }

    /// <summary>
    /// The payments that repeat, oldest first.
    /// </summary>
    [Description("The payments behind the suggestion, oldest first")]
    public List<SuggestedPayment> Payments { get; init; } = [];
}
