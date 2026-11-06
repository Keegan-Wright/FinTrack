using FinanceTracker.Client.Layout;
using FinanceTracker.Contracts.Api;

namespace FinanceTracker.Api;

public static class AppVersionHeaderExtensions
{
    extension(WebApplication app)
    {
        /// <summary>
        /// Added on every api response which build of the app the server serves, see
        /// <see cref="ApiHeaders.Version"/>. It's the front end's, the one a tab loading now would get.
        /// </summary>
        public WebApplication UseAppVersionHeader()
        {
            app.Use((context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
                {
                    // Once the answer is under way, the exception handler clears headers set before it
                    context.Response.OnStarting(() =>
                    {
                        context.Response.Headers[ApiHeaders.Version] = AppVersion.Full;
                        return Task.CompletedTask;
                    });
                }

                return next(context);
            });

            return app;
        }
    }
}
