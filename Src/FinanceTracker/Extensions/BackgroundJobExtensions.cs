using FinanceTracker.BackgroundJobs;
using FinanceTracker.Infrastructure;
using TickerQ.DependencyInjection;
using TickerQ.EntityFrameworkCore.Customizer;
using TickerQ.EntityFrameworkCore.DependencyInjection;
using TickerQ.Instrumentation.OpenTelemetry;

namespace FinanceTracker.Extensions;

public static class BackgroundJobExtensions
{
    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// TickerQ, the jobs it runs, and the notifier jobs tell users' open tabs about finished syncs through.
        /// </summary>
        public WebApplicationBuilder AddBackgroundJobs()
        {
            builder.Services.AddTickerQ(options =>
            {
                options.AddOpenTelemetryInstrumentation();

                options.ConfigureScheduler(schedulerOptions => { schedulerOptions.SchedulerTimeZone = TimeZoneInfo.Utc; });

                options.AddOperationalStore(efOptions =>
                {
                    efOptions.UseApplicationDbContext<FinanceTrackerContext>(ConfigurationType.IgnoreModelCustomizer);
                });

                options.IgnoreSeedDefinedCronTickers();
            });

            builder.Services.MapTicker<SyncAllOpenBankingDetailsAsync>();
            builder.Services.MapTicker<SyncUserOpenBankingDetailsAsync, SyncUserOpenBankingDetailsRequest>();
            builder.Services.MapTicker<MonitorOpenBankingTokenExpirationAsync>();

            builder.Services.AddSingleton<ISyncNotifier, SyncNotifier>();

            return builder;
        }
    }
}
