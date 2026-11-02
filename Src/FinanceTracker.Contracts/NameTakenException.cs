namespace FinanceTracker.Contracts;

/// <summary>
/// The user already has a <see cref="Kind"/> called <see cref="Name"/>, in any case, e.g. a tag or a savings goal.
/// </summary>
public sealed class NameTakenException : Exception
{
    /// <summary>
    /// <see cref="Kind"/> for the user's custom classifications.
    /// </summary>
    public const string Tag = "tag";

    /// <summary>
    /// <see cref="Kind"/> for the people in the user's household.
    /// </summary>
    public const string Person = "person";

    /// <summary>
    /// <see cref="Kind"/> for the user's savings goals.
    /// </summary>
    public const string SavingsGoal = "savings goal";

    /// <summary>
    /// <see cref="Kind"/> for the user's debts, paid off or not.
    /// </summary>
    public const string Debt = "debt";

    /// <param name="kind">What's named, as it reads in a sentence, e.g. <c>savings goal</c>.</param>
    /// <param name="name">The name that's taken, as the user typed it.</param>
    public NameTakenException(string kind, string name) : base($"You already have a {kind} called {name}")
    {
        Kind = kind;
        Name = name;
    }

    /// <summary>
    /// What's named, e.g. <c>savings goal</c>.
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// The name that's taken.
    /// </summary>
    public string Name { get; }
}
