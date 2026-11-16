using FinanceTracker.Domain.Identity;

namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// How a person reads to someone they share with.
/// </summary>
internal static class PersonNames
{
    /// <summary>
    /// <paramref name="user"/>'s first and last name, e.g. <c>Jane Doe</c>.
    /// </summary>
    public static string Full(FinanceTrackerUser user) => $"{user.FirstName} {user.LastName}".Trim();
}
