using FinanceTracker.Contracts.Diagnostics;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// Errors the app in the browser hit, reported to the server, which logs them against the user signed in.
/// </summary>
public interface IClientErrorsService
{
    /// <summary>
    /// Reports <paramref name="error"/>.
    /// </summary>
    Task ReportAsync(ClientError error, CancellationToken cancellationToken);
}
