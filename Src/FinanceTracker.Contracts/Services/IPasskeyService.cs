using FinanceTracker.Contracts.Profile;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The signed in user's passkeys, for signing in with a fingerprint, face or PIN instead of a password. Only for
/// password users, as one would let someone who signs in through the identity provider get round it. Each method is
/// null when the user has no password here.
/// </summary>
public interface IPasskeyService
{
    /// <summary>
    /// The user's passkeys, newest first.
    /// </summary>
    /// <param name="cancellationToken">Stops the work.</param>
    Task<IReadOnlyList<PasskeySummary>?> GetPasskeysAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Starts adding a passkey once the user's password is right, giving what the browser needs to make one.
    /// </summary>
    /// <param name="request">The user's password.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    Task<PasskeyChange?> StartAddingAsync(ConfirmPassword request, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the passkey the browser made after <see cref="StartAddingAsync"/>. The user is emailed to say so.
    /// </summary>
    /// <param name="request">The browser's credential and its name.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    Task<PasskeyChange?> AddAsync(AddPasskey request, CancellationToken cancellationToken);

    /// <summary>
    /// Renames one of the user's passkeys.
    /// </summary>
    /// <param name="id">The passkey's id.</param>
    /// <param name="request">Its new name.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    Task<PasskeyChange?> RenameAsync(string id, RenamePasskey request, CancellationToken cancellationToken);

    /// <summary>
    /// Removes one of the user's passkeys once their password is right. The user is emailed to say so.
    /// </summary>
    /// <param name="id">The passkey's id.</param>
    /// <param name="request">The user's password.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    Task<PasskeyChange?> RemoveAsync(string id, ConfirmPassword request, CancellationToken cancellationToken);
}
