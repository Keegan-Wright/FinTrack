using System.Globalization;

namespace FinanceTracker.Tests.Unit.Client;

/// <summary>
/// Runs the rest of a test in <paramref name="name"/>'s culture, the app's being the server's. Restored on dispose.
/// </summary>
public sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public CultureScope(string name = "en-GB") => CultureInfo.CurrentCulture = new CultureInfo(name);

    public void Dispose() => CultureInfo.CurrentCulture = _culture;
}
