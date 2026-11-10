namespace FinanceTracker.Infrastructure.Rules;

/// <summary>
/// What one run of a user's rules did, for its log and span.
/// </summary>
/// <param name="Rules">The enabled rules that ran.</param>
/// <param name="Considered">The transactions they ran over.</param>
/// <param name="Changed">The transactions they changed.</param>
/// <param name="Matches">How many transactions each rule matched, by rule id, only rules that matched any.</param>
internal sealed record RuleRun(int Rules, int Considered, int Changed, IReadOnlyDictionary<Guid, int> Matches)
{
    public static RuleRun Nothing { get; } = new(0, 0, 0, new Dictionary<Guid, int>());
}
