namespace FinanceTracker.Infrastructure;

public static class TimeProviderExtensions
{
    extension(TimeProvider timeProvider)
    {
        /// <summary>
        /// Now in UTC, as times are stored.
        /// </summary>
        public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

        /// <summary>
        /// Today where the server is, the user's day for budgets, bills and balance snapshots.
        /// </summary>
        public DateOnly Today => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
    }
}
