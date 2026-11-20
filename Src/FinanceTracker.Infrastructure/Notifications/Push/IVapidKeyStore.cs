namespace FinanceTracker.Infrastructure.Notifications.Push;

/// <summary>
/// The server's <see cref="VapidKeys"/>.
/// </summary>
public interface IVapidKeyStore
{
    /// <summary>
    /// The key pair, made and saved the first time it's asked for.
    /// </summary>
    /// <param name="cancellationToken">Stops the work.</param>
    Task<VapidKeys> GetAsync(CancellationToken cancellationToken);
}
