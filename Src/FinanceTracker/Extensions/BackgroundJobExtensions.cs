using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.BackgroundJobs;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Infrastructure.Automation;
using Quartz;

namespace FinanceTracker.Extensions;

public static class BackgroundJobExtensions
{
    /// <summary>
    /// How long the automation page's run history goes back.
    /// </summary>
    private static readonly TimeSpan HistoryRetention = TimeSpan.FromDays(90);

    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// Quartz, keeping its schedules and run history in the app's Postgres database, creating its tables when they
        /// aren't there, and the jobs it runs. Then the notifier jobs tell users' browsers about finished syncs.
        /// </summary>
        public WebApplicationBuilder AddBackgroundJobs()
        {
            string connectionString = builder.Configuration.GetFinTrackDatabaseConnectionString();

            builder.Services.AddQuartzExecutionHistory(static options =>
            {
                options.Retention = HistoryRetention;
                options.MaxEntriesPerScheduler = 20_000;
            });

            builder.Services.AddQuartz(quartz =>
            {
                quartz.UseDefaultTimeZone(TimeZoneInfo.Utc);

                quartz.UsePersistentStore(store =>
                {
                    store.UsePostgres(connectionString);
                    store.ProvisionSchema();
                    store.UseExecutionHistory();
                    // Job data as strings, which avoids surprises when a serialised type changes shape later
                    store.ConfigureStore(static options => options.StoreJobDataAsStrings = true);
                });

                quartz.AddJob<SyncAllOpenBankingDetailsAsync>(static job =>
                    job.WithIdentity(JobNames.SyncAll)
                    .StoreDurably());

                quartz.AddJob<SyncUserOpenBankingDetailsAsync>(static job =>
                    job.WithIdentity(JobNames.SyncUser)
                    .StoreDurably());

                quartz.AddJob<MonitorOpenBankingTokenExpirationAsync>(static job =>
                    job.WithIdentity(JobNames.MonitorTokens)
                        .StoreDurably());

                quartz.AddJob<MoveEventSavingsGoalsOnAsync>(static job =>
                    job.WithIdentity(JobNames.MoveGoalsOn)
                        .StoreDurably());

                // The schedules each job starts with, stored the first time the app starts and passed over after,
                // so changes made on the automation page are kept, see the IgnoreDuplicates below
                foreach (ScheduledJob job in ScheduledJobs.All)
                {
                    quartz.AddTrigger(trigger => JobSchedules.Configure(trigger, job.Function, job.Description,
                        job.Expression, job.Retries, job.RetryIntervals));
                }

                quartz.AddJobListener<JobChangedListener>([]);

                // Each run's logs carry the job and firing, exported with the scopes OpenTelemetry includes
                quartz.AddJobLogScope();
            });
            builder.Services.Configure<QuartzOptions>(static options => options.Scheduling.IgnoreDuplicates = true);
            builder.Services.AddQuartzHostedService(static options => options.WaitForJobsToComplete = true);

            builder.Services.AddSingleton<ISyncNotifier, SyncNotifier>();
            builder.Services.AddScoped<ISyncNotifications, PrerenderSyncNotifications>();

            return builder;
        }
    }
}
