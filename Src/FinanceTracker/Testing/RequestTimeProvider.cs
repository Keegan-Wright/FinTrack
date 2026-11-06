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

    public RequestTimeProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + Offset();

    public override TimeZoneInfo LocalTimeZone => System.LocalTimeZone;

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
