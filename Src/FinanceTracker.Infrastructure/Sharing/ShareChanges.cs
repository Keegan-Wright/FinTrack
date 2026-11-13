using FinanceTracker.Domain.Sharing;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// What one person chose to share in a link before and after a change.
/// </summary>
internal static class ShareChanges
{
    /// <summary>
    /// What <paramref name="userId"/> chose to share in <paramref name="linkId"/>, and how, by kind and item. Not
    /// what's shared because it's on a shared transaction, which comes and goes with the transactions.
    /// </summary>
    public static async Task<Dictionary<(ShareItemType Kind, Guid ItemId), ShareMode>> ChosenAsync(
        FinanceTrackerContext context, Guid linkId, Guid userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        List<Share> shares = await context.Shares.AsNoTracking()
            .Where(x => x.LinkId == linkId && x.UserId == userId && x.Reason == ShareReason.Chosen)
            .ToListAsync(cancellationToken);
        return shares.ToDictionary(static x => (x.ItemType, x.ItemId), static x => x.Mode);
    }

    /// <summary>
    /// What changed from <paramref name="before"/> to <paramref name="after"/>, one sentence for each of shared,
    /// stopped, now changeable and now only seen. Null when nothing did.
    /// </summary>
    public static string? Describe(Dictionary<(ShareItemType Kind, Guid ItemId), ShareMode> before,
        Dictionary<(ShareItemType Kind, Guid ItemId), ShareMode> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        List<ShareItemType> added = after.Keys.Where(x => !before.ContainsKey(x)).Select(static x => x.Kind).ToList();
        List<ShareItemType> removed =
            before.Keys.Where(x => !after.ContainsKey(x)).Select(static x => x.Kind).ToList();
        List<ShareItemType> canChangeNow = [];
        List<ShareItemType> seeOnlyNow = [];
        foreach (((ShareItemType Kind, Guid ItemId) key, ShareMode mode) in after)
        {
            if (!before.TryGetValue(key, out ShareMode was) || was == mode)
            {
                continue;
            }

            if (mode == ShareMode.AllowChanges)
            {
                canChangeNow.Add(key.Kind);
            }
            else
            {
                seeOnlyNow.Add(key.Kind);
            }
        }

        List<string> sentences = [];
        AddSentence(sentences, added, "They shared {0} with you.");
        AddSentence(sentences, removed, "They stopped sharing {0}.");
        AddSentence(sentences, canChangeNow, "You can change {0} now.");
        AddSentence(sentences, seeOnlyNow, "You can only see {0} now.");
        if (sentences.Count == 0)
        {
            return null;
        }

        return string.Join(" ", sentences);
    }

    /// <summary>
    /// Adds <paramref name="sentence"/> naming how many of each kind <paramref name="kinds"/> holds, when any.
    /// </summary>
    private static void AddSentence(List<string> sentences, List<ShareItemType> kinds, string sentence)
    {
        if (kinds.Count == 0)
        {
            return;
        }

        List<string> counts = kinds
            .GroupBy(static x => x)
            .OrderBy(static x => x.Key)
            .Select(static x => CountText(x.Count(), KindText(x.Key)))
            .ToList();
        sentences.Add(string.Format(System.Globalization.CultureInfo.CurrentCulture, sentence, ListText(counts)));
    }

    /// <summary>
    /// One of a kind, as it reads in a sentence, e.g. <c>savings goal</c>.
    /// </summary>
    private static string KindText(ShareItemType kind) => kind switch
    {
        ShareItemType.BankAccount => "account",
        ShareItemType.Classification => "tag",
        ShareItemType.Bill => "bill",
        ShareItemType.Debt => "debt",
        ShareItemType.Rule => "rule",
        ShareItemType.Budget => "budget",
        ShareItemType.SavingsGoal => "savings goal",
        ShareItemType.CalendarEvent => "calendar event",
        _ => "item"
    };

    private static string CountText(int count, string what)
    {
        if (count == 1)
        {
            return $"1 {what}";
        }

        return $"{count} {what}s";
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
