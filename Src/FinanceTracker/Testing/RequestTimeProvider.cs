using System.Globalization;
using FinanceTracker.AppHost.ServiceDefaults;

namespace FinanceTracker.Testing;

/// <summary>
/// The real clock moved by the request's <see cref="TestClock.Cookie"/>, so a test can see the app on a day of its
/// choosing. Only registered when <see cref="FinTrackEnvironment.TestClock"/> is on, see
/// <c>TestClockExtensions</c>. Outside a request, e.g. a background job, it's the real clock.
/// </summary>
public sealed class RequestTimeProvider : TimeProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <param name="httpContextAccessor">Gives the request, whose cookie moves the clock.</param>
    public RequestTimeProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + Offset();

    /// <inheritdoc/>
    public override TimeZoneInfo LocalTimeZone => System.LocalTimeZone;

    /// <summary>
    /// How far the request's test clock cookie moves the clock, in seconds. Nothing without one.
    /// </summary>
    private TimeSpan Offset()
    {
        string? cookie = _httpContextAccessor.HttpContext?.Request.Cookies[TestClock.Cookie];
        if (!long.TryParse(cookie, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long seconds))
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromSeconds(seconds);
    }
}
