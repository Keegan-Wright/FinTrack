using Microsoft.AspNetCore.Diagnostics;

namespace FinanceTracker.Components.Pages;

/// <summary>
/// Where the error page's Try again goes, the address that failed, when going there again is only asking for a page
/// again.
/// </summary>
public static class ErrorRetry
{
    /// <summary>
    /// The failed request's path and query, <see langword="null"/> when the error page was visited itself or the
    /// request was sending something, e.g. a form, which going to its address wouldn't send again.
    /// </summary>
    public static string? PathFor(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IExceptionHandlerPathFeature? failed = context.Features.Get<IExceptionHandlerPathFeature>();
        if (failed is null || !HttpMethods.IsGet(context.Request.Method))
        {
            return null;
        }

        // The exception handler runs this page in the failed request, which keeps its query
        return $"{failed.Path}{context.Request.QueryString}";
    }
}
