namespace FinanceTracker.Contracts.HouseholdMember;

/// <summary>
/// A household member after saving how to recognise them, and what that recognised.
/// </summary>
public class HouseholdMemberRecognitionSaved
{
    /// <summary>
    /// The member as stored.
    /// </summary>
    public required HouseholdMember Member { get; init; }

    /// <summary>
    /// How many of the user's transactions now count as money moved to or from the member.
    /// </summary>
    public required int RecognisedTransactions { get; init; }
}
