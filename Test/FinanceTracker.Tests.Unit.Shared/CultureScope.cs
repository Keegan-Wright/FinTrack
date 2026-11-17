using System.Globalization;

namespace FinanceTracker.Tests.Unit.Shared;

/// <summary>
/// Runs the rest of a test in another culture, the app's being the server's. Restored on dispose.
/// </summary>
public sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    /// <param name="name">The culture to run in, e.g. <c>en-GB</c>.</param>
    public CultureScope(string name = "en-GB") => CultureInfo.CurrentCulture = new CultureInfo(name);

    public void Dispose() => CultureInfo.CurrentCulture = _culture;
}
