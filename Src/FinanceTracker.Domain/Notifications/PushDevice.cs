using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Notifications;

/// <summary>
/// A browser a user gets push notifications on, its push subscription. Where its push service takes messages for it,
/// and the keys only it can read them with. Signing out leaves it, removing it stops the pushes.
/// </summary>
public sealed class PushDevice : AggregateRoot
{
    /// <summary>
    /// The longest a device's name can be.
    /// </summary>
    public const int MostNameLength = 60;

    [Obsolete("EF Use Only")]
    private PushDevice()
    {
    }

    /// <summary>
    /// A browser that has just subscribed.
    /// </summary>
    /// <param name="userId">The user it's for.</param>
    /// <param name="endpoint">Where its push service takes messages for it.</param>
    /// <param name="publicKey">Its <c>p256dh</c> key, base64url.</param>
    /// <param name="authSecret">Its <c>auth</c> secret, base64url.</param>
    /// <param name="name">What it is, e.g. <c>Chrome on Android</c>.</param>
    /// <exception cref="ArgumentException">A value is blank, or the name too long.</exception>
    public PushDevice(Guid userId, string endpoint, string publicKey, string authSecret, string name) : base(userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        Endpoint = endpoint;
        Renew(publicKey, authSecret, name);
    }

    /// <summary>
    /// Where its push service takes messages for it.
    /// </summary>
    public string Endpoint { get; private set; } = null!;

    /// <summary>
    /// Its <c>p256dh</c> key, base64url, which messages are encrypted to.
    /// </summary>
    public string PublicKey { get; private set; } = null!;

    /// <summary>
    /// Its <c>auth</c> secret, base64url, mixed into each message's key.
    /// </summary>
    public string AuthSecret { get; private set; } = null!;

    /// <summary>
    /// What it is, e.g. <c>Chrome on Android</c>, so the user can tell their devices apart.
    /// </summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    /// Last tried pushing to it, null when it never has.
    /// </summary>
    public DateTime? LastPushAt { get; private set; }

    /// <summary>
    /// Why that push failed, in words for the user, null when it was delivered.
    /// </summary>
    public string? LastPushFailure { get; private set; }

    /// <summary>
    /// Takes the keys and name the browser gave when it subscribed again, e.g. after its old subscription ran out.
    /// </summary>
    /// <param name="publicKey">Its <c>p256dh</c> key, base64url.</param>
    /// <param name="authSecret">Its <c>auth</c> secret, base64url.</param>
    /// <param name="name">What it is.</param>
    /// <exception cref="ArgumentException">A value is blank, or the name too long.</exception>
    public void Renew(string publicKey, string authSecret, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(authSecret);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string trimmedName = name.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(trimmedName.Length, MostNameLength, nameof(name));

        PublicKey = publicKey;
        AuthSecret = authSecret;
        Name = trimmedName;
    }

    /// <summary>
    /// Records a push delivered at <paramref name="at"/>, or that it failed and why.
    /// </summary>
    /// <param name="at">When it was pushed or tried.</param>
    /// <param name="failure">Why it failed, null when it was delivered.</param>
    public void RecordPush(DateTime at, string? failure)
    {
        LastPushAt = at;
        LastPushFailure = string.IsNullOrWhiteSpace(failure) ? null : failure.Trim();
    }
}
