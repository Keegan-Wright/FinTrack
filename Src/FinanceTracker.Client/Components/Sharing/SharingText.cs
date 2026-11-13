using FinanceTracker.Contracts.Sharing;

namespace FinanceTracker.Client.Components.Sharing;

/// <summary>
/// How what's shared in a link reads, on the Sharing pages.
/// </summary>
public static class SharingText
{
    /// <summary>
    /// What a mode lets the other person do, e.g. <c>Can see and change</c>.
    /// </summary>
    public static string ModeText(ShareMode mode) => mode switch
    {
        ShareMode.AllowChanges => "Can see and change",
        _ => "Can see"
    };

    /// <summary>
    /// What kind of thing something shared is, singular, e.g. <c>Tag</c>.
    /// </summary>
    public static string KindText(SharedItemKind kind) => kind switch
    {
        SharedItemKind.Classification => "Tag",
        SharedItemKind.Rule => "Rule",
        SharedItemKind.Bill => "Bill",
        SharedItemKind.Debt => "Debt",
        SharedItemKind.Budget => "Budget",
        SharedItemKind.SavingsGoal => "Goal",
        SharedItemKind.CalendarEvent => "Event",
        _ => "Item"
    };

    /// <summary>
    /// What a thing going with a shared account says, e.g. <c>Goes with Joint · can see</c>.
    /// </summary>
    public static string FollowsText(string account, ShareMode mode) =>
        $"Goes with {account} · {ModeText(mode).ToLowerInvariant()}";

    /// <summary>
    /// <paramref name="count"/> of <paramref name="what"/>, e.g. <c>1 tag</c> or <c>3 tags</c>.
    /// </summary>
    public static string CountText(int count, string what)
    {
        if (count == 1)
        {
            return $"1 {what}";
        }

        return $"{count} {what}s";
    }

    /// <summary>
    /// How many accounts and other things they share once saved, e.g. <c>Sharing 2 accounts, 3 tags and 1 rule with
    /// Jane</c>.
    /// </summary>
    public static string SharedText(LinkShares shares)
    {
        ArgumentNullException.ThrowIfNull(shares);
        List<string> parts = [];
        int accounts = shares.YourAccounts.Count(static x => x.Mode is not null);
        if (accounts > 0)
        {
            parts.Add(CountText(accounts, "account"));
        }

        AddCount(parts, shares, SharedItemKind.Classification, "tag");
        AddCount(parts, shares, SharedItemKind.Rule, "rule");
        AddCount(parts, shares, SharedItemKind.Bill, "bill");
        AddCount(parts, shares, SharedItemKind.Debt, "debt");
        AddCount(parts, shares, SharedItemKind.Budget, "budget");
        AddCount(parts, shares, SharedItemKind.SavingsGoal, "goal");
        AddCount(parts, shares, SharedItemKind.CalendarEvent, "event");
        if (parts.Count == 0)
        {
            return $"Nothing shared with {shares.Name}";
        }

        return $"Sharing {ListText(parts)} with {shares.Name}";
    }

    /// <summary>
    /// How many changes wait to be saved, e.g. <c>3 changes</c>.
    /// </summary>
    public static string ChangesText(int changes) => CountText(changes, "change");

    /// <summary>
    /// The other person's copy of a joint account that could be merged, e.g. <c>The same account as their Joint</c>.
    /// </summary>
    public static string SuggestionText(JointAccountSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(suggestion);
        return $"The same account as their {suggestion.TheirAccountName}";
    }

    /// <summary>
    /// Whose copy of a merged joint account both see, e.g. <c>Counted once, from your connection</c>.
    /// </summary>
    public static string MergedText(MergedJointAccount joint, string name)
    {
        ArgumentNullException.ThrowIfNull(joint);
        if (joint.ShownIsYours)
        {
            return "Counted once, from your connection";
        }

        return $"Counted once, from {name}'s connection";
    }

    /// <summary>
    /// What merging did, e.g. <c>Merged Christmas, it's counted once now</c>.
    /// </summary>
    public static string MergeDoneText(IReadOnlyList<JointAccountSuggestion> suggestions)
    {
        ArgumentNullException.ThrowIfNull(suggestions);
        if (suggestions.Count == 1)
        {
            return $"Merged {suggestions[0].YourAccountName}, it's counted once now";
        }

        return $"Merged {suggestions.Count} accounts, each is counted once now";
    }

    /// <summary>
    /// Adds how many of <paramref name="kind"/> they share to <paramref name="parts"/>, when any.
    /// </summary>
    private static void AddCount(List<string> parts, LinkShares shares, SharedItemKind kind, string what)
    {
        int count = shares.YourItems.Count(x => x.Kind == kind && x.Mode is not null);
        if (count > 0)
        {
            parts.Add(CountText(count, what));
        }
    }

    /// <summary>
    /// <paramref name="parts"/> as a list, e.g. <c>a, b and c</c>.
    /// </summary>
    private static string ListText(List<string> parts)
    {
        if (parts.Count == 1)
        {
            return parts[0];
        }

        return $"{string.Join(", ", parts[..^1])} and {parts[^1]}";
    }
}
