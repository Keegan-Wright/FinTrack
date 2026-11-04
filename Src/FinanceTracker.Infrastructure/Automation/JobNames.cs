namespace FinanceTracker.Infrastructure.Automation;

/// <summary>
/// The background jobs by the names Quartz keeps them under, the host's job classes are named the same.
/// </summary>
public static class JobNames
{
    /// <summary>
    /// A sync of every user's connections, on a schedule.
    /// </summary>
    public const string SyncAll = "SyncAllOpenBankingDetailsAsync";

    /// <summary>
    /// A sync of one user's connections, asked for by them, see <see cref="UserIdKey"/>.
    /// </summary>
    public const string SyncUser = "SyncUserOpenBankingDetailsAsync";

    /// <summary>
    /// Revokes tokens whose consent has lapsed, on a schedule.
    /// </summary>
    public const string MonitorTokens = "MonitorOpenBankingTokenExpirationAsync";

    /// <summary>
    /// Moves yearly events' savings goals on, on a schedule.
    /// </summary>
    public const string MoveGoalsOn = "MoveEventSavingsGoalsOnAsync";

    /// <summary>
    /// The trigger group a job's schedule is kept in, under the job's name.
    /// </summary>
    public const string ScheduleGroup = "schedules";

    /// <summary>
    /// The job data key for the user a <see cref="SyncUser"/> run syncs.
    /// </summary>
    public const string UserIdKey = "userId";
}
