using System.ComponentModel;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Contracts.Rules;

/// <summary>
/// One transaction applying a rule would change, and how.
/// </summary>
public class RulePreviewChange
{
    /// <summary>
    /// The transaction's id.
    /// </summary>
    [Description("The transaction's id")]
    public Guid TransactionId { get; init; }

    /// <summary>
    /// The transaction's description.
    /// </summary>
    [Description("The transaction's description")]
    public required string Description { get; init; }

    /// <summary>
    /// The transaction's amount, negative for money out.
    /// </summary>
    [Description("The transaction's amount, negative for money out")]
    public decimal Amount { get; init; }

    /// <summary>
    /// When it happened.
    /// </summary>
    [Description("When the transaction happened")]
    public DateTime TransactionTime { get; init; }

    /// <summary>
    /// Tags it would gain, by name.
    /// </summary>
    [Description("Tags it would gain")]
    public List<string> TagsAdded { get; init; } = [];

    /// <summary>
    /// Tags it would lose, by name.
    /// </summary>
    [Description("Tags it would lose")]
    public List<string> TagsRemoved { get; init; } = [];

    /// <summary>
    /// The bank's classifications it would hide.
    /// </summary>
    [Description("The bank's classifications it would hide")]
    public List<string> ClassificationsHidden { get; init; } = [];

    /// <summary>
    /// The bank's classifications it would show again.
    /// </summary>
    [Description("The bank's classifications it would show again")]
    public List<string> ClassificationsShown { get; init; } = [];

    /// <summary>
    /// How it counts now, null when nothing has decided.
    /// </summary>
    [Description("How it counts now")]
    public TransferState? TransferBefore { get; init; }

    /// <summary>
    /// How it would count, null when nothing would decide.
    /// </summary>
    [Description("How it would count")]
    public TransferState? TransferAfter { get; init; }
}
