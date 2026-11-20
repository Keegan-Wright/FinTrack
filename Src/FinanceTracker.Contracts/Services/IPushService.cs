using FinanceTracker.Contracts.Notifications;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The signed in user's push notifications. The browsers they get them on, and a test.
/// </summary>
public interface IPushService
{
    /// <summary>
    /// Their devices, and the key a browser subscribes with.
    /// </summary>
    Task<PushSettings> GetSettingsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Pushes to the browser that gave <paramref name="device"/> from now on. A browser already added is updated,
    /// and one another user added is theirs no more, as it has changed hands.
    /// </summary>
    /// <returns>The settings, and the device's id, the one to keep for this browser.</returns>
    Task<PushDeviceAdded> AddDeviceAsync(AddPushDevice device, CancellationToken cancellationToken);

    /// <summary>
    /// Stops pushing to one of their devices.
    /// </summary>
    /// <returns>The settings, null when they have no device <paramref name="id"/>.</returns>
    Task<PushSettings?> RemoveDeviceAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Changes whether pushes say amounts on a locked phone's screen.
    /// </summary>
    /// <returns>The settings.</returns>
    Task<PushSettings> ChangeLockScreenAsync(ChangeLockScreen lockScreen, CancellationToken cancellationToken);

    /// <summary>
    /// Pushes a test to each of their devices. Any whose browser has unsubscribed is removed.
    /// </summary>
    /// <returns>The settings with how each push went, null when they have no devices.</returns>
    Task<PushSettings?> SendTestAsync(CancellationToken cancellationToken);
}
