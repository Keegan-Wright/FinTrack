using System.Data.Common;
using FinanceTracker.Contracts;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure;

/// <summary>
/// Keeps the names of a user's tags, people and savings goals apart, so each picker and label is unambiguous. Names
/// are encrypted, so they're compared here once decrypted. The database backs it up with a unique index on each
/// table's <see cref="NameBlindIndex"/>, for two saves at once, see <see cref="SaveKeepingNamesApartAsync"/>.
/// </summary>
public static class NameCheck
{
    /// <summary>
    /// Throws when another of <paramref name="existing"/> is called <paramref name="name"/>, ignoring case and
    /// spaces at either end.
    /// </summary>
    /// <param name="existing">The user's records of this kind, by id and name.</param>
    /// <param name="exceptId">The record being renamed, which can keep its own name in a different case.</param>
    /// <param name="kind">What's named, for the message, e.g. <c>savings goal</c>.</param>
    /// <exception cref="NameTakenException">Another record has the name.</exception>
    public static void EnsureFree(IEnumerable<(Guid Id, string Name)> existing, string name, Guid? exceptId,
        string kind)
    {
        string wanted = NameBlindIndex.Normalise(name);
        bool taken = existing.Any(x => x.Id != exceptId && NameBlindIndex.Normalise(x.Name) == wanted);
        if (taken)
        {
            throw new NameTakenException(kind, name.Trim());
        }
    }

    /// <summary>
    /// Saves, turning the database refusing a name another save took a moment ago into the same
    /// <see cref="NameTakenException"/> <see cref="EnsureFree"/> throws.
    /// </summary>
    /// <param name="kind">What's named, for the message, e.g. <c>savings goal</c>.</param>
    /// <param name="name">The name being saved.</param>
    /// <exception cref="NameTakenException">A unique index on a name key refused it.</exception>
    public static async Task SaveKeepingNamesApartAsync(FinanceTrackerContext context, string kind, string name,
        CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsNameTaken(ex))
        {
            throw new NameTakenException(kind, name.Trim());
        }
    }

    /// <summary>
    /// Whether a unique index on a name key refused the save. Postgres names the index and SQLite the columns, both
    /// with <see cref="NameBlindIndex.PropertyName"/> in them.
    /// </summary>
    private static bool IsNameTaken(DbUpdateException exception) =>
        exception.InnerException is DbException database &&
        database.Message.Contains(NameBlindIndex.PropertyName, StringComparison.Ordinal);
}
