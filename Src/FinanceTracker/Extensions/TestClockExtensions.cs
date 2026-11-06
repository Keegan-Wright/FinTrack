using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.Testing;

namespace FinanceTracker.Extensions;

public static class TestClockExtensions
{
    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// The app's clock: the real one, or once <see cref="FinTrackEnvironment.TestClock"/> is on, one each request
        /// can move for the integration tests (<see cref="RequestTimeProvider"/>).
        /// </summary>
        public WebApplicationBuilder AddFinTrackClock()
        {
            if (!builder.Configuration.GetValue<bool>(FinTrackEnvironment.TestClock))
            {
                builder.Services.AddSingleton(TimeProvider.System);
                return builder;
            }

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSingleton<TimeProvider, RequestTimeProvider>();
            return builder;
        }
    }
}
