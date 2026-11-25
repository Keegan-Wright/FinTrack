using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Common.Persistence;

/// <summary>
/// Reaches a context's encryption from places that only have the <see cref="DbContext"/>.
/// </summary>
internal static class EncryptedContexts
{
    /// <summary>
    /// <paramref name="context"/>'s encryption, which every context with encrypted columns or tagged rows has.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <exception cref="InvalidOperationException">The context isn't an <see cref="IEncryptedContext"/>.</exception>
    public static IEncryptedContext Of(DbContext context) =>
        context as IEncryptedContext ?? throw new InvalidOperationException(
            $"{context.GetType().Name} has encrypted columns or tagged rows but isn't an {nameof(IEncryptedContext)}.");
}
