using Microsoft.AspNetCore.Http;

namespace FinanceTracker.AppHost.ServiceDefaults;

/// <summary>
/// Which requests get a span. Health checks and static files are left out, they'd bury the requests worth reading
/// loading the app fetches its WebAssembly files one by one.
/// </summary>
public static class TraceFilters
{
    private static readonly PathString[] Untraced =
    [
        Extensions.HealthEndpointPath,
        Extensions.AlivenessEndpointPath,
        "/_framework",
        "/_content"
    ];

    /// <summary>
    /// Whether <paramref name="context"/>'s request gets a span not a health check, and not a file such as
    /// <c>/app.css</c>. Pages and the API have no extension.
    /// </summary>
    public static bool IsTraced(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        PathString path = context.Request.Path;

        foreach (PathString untraced in Untraced)
        {
            if (path.StartsWithSegments(untraced))
            {
                return false;
            }
        }

        return !Path.HasExtension(path.Value);
    }
}
