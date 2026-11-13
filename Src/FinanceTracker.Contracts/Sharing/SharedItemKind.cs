namespace FinanceTracker.Contracts.Sharing;

/// <summary>
/// The kinds of thing besides accounts a user can choose to share in a link.
/// </summary>
public enum SharedItemKind
{
    /// <summary>
    /// A tag. Shared, the other person can put it on their own transactions too, and rename it when allowed changes.
    /// </summary>
    Classification = 0,

    /// <summary>
    /// A rule. Shared, the other person sees it on their Rules page, and edits it too when allowed changes. It still
    /// runs where its owner's rules run.
    /// </summary>
    Rule = 1,

    /// <summary>
    /// A bill. One paid from an account shared with the other person is shared with it, in the same way. Others can be
    /// chosen. Shared, the other person sees it, and edits it too when allowed changes.
    /// </summary>
    Bill = 2,

    /// <summary>
    /// A debt. One following an account shared with the other person, a loan or card, is shared with it, in the same
    /// way. Others can be chosen. Shared, the other person sees it, and edits it too when allowed changes.
    /// </summary>
    Debt = 3,

    /// <summary>
    /// A budget. Shared, it's a household budget, counting tagged spending on every account its owner sees, and the
    /// other person sees it, or edits it too when allowed changes.
    /// </summary>
    Budget = 4,

    /// <summary>
    /// A savings goal. One tracking an account shared with the other person goes with it, in the same way. Others can
    /// be chosen.
    /// </summary>
    SavingsGoal = 5,

    /// <summary>
    /// A calendar event, shown on the other person's calendar too.
    /// </summary>
    CalendarEvent = 6
}
