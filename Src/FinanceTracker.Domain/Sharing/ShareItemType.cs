namespace FinanceTracker.Domain.Sharing;

/// <summary>
/// What kind of thing a <see cref="Share"/> shares. More kinds follow as each feature learns to share.
/// </summary>
public enum ShareItemType
{
    /// <summary>
    /// A bank account or card, with its transactions, balances and history.
    /// </summary>
    BankAccount = 0,

    /// <summary>
    /// A custom classification, a tag. Chosen, the other person can use it on their own transactions too, and rename
    /// it if allowed. Or read only, when it's put on the other person's transaction so they see its name.
    /// </summary>
    Classification = 1,

    /// <summary>
    /// A bill, read only, shared when the other person's transaction is said to pay it so they see its name.
    /// </summary>
    Bill = 2,

    /// <summary>
    /// A debt, read only, shared when the other person's transaction is said to pay it off so they see its name.
    /// </summary>
    Debt = 3,

    /// <summary>
    /// A rule, chosen. The other person sees it, and edits it too when allowed changes. It stays its owner's, so it
    /// runs where its owner's rules run.
    /// </summary>
    Rule = 4,

    /// <summary>
    /// A budget, chosen. A shared budget is a household one, counting tagged spending on every account its owner sees.
    /// </summary>
    Budget = 5,

    /// <summary>
    /// A savings goal. One tracking an account shared with the other person goes with it, others are chosen.
    /// </summary>
    SavingsGoal = 6,

    /// <summary>
    /// A calendar event, chosen.
    /// </summary>
    CalendarEvent = 7
}
