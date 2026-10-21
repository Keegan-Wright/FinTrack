using System.Reflection;
using TickerQ.Utilities.Base;

namespace FinanceTracker.Tests.Unit.FinanceTracker.BackgroundJobs;

/// <summary>
/// Builds the context TickerQ passes to a job. TickerQ only creates these internally, so the cron operations a job
/// calls into are wired up by reflection.
/// </summary>
internal static class TickerFunctionContextFactory
{
    public static TickerFunctionContext Create()
    {
        var context = new TickerFunctionContext();

        if (context.CronOccurrenceOperations is null)
        {
            FieldInfo? backingField = typeof(TickerFunctionContext)
                .GetField("<CronOccurrenceOperations>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            backingField?.SetValue(context, new CronOccurrenceOperations());
        }

        PropertyInfo? skipActionProperty = typeof(CronOccurrenceOperations)
            .GetProperty("SkipIfAlreadyRunningAction", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        skipActionProperty?.SetValue(context.CronOccurrenceOperations, (Action)(() => { }));

        return context;
    }
}
