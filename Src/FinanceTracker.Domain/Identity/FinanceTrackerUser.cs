using Microsoft.AspNetCore.Identity;

namespace FinanceTracker.Domain.Identity;

/// <summary>
/// Users of the application
/// </summary>
public sealed class FinanceTrackerUser : IdentityUser<Guid>
{
    /// <summary>
    /// The <see cref="IdentityUser{TKey}.LockoutEnd"/> of a user an admin has locked, which never ends. Identity's own
    /// lockout after wrong passwords ends after a few minutes, so the two can be told apart.
    /// </summary>
    public static readonly DateTimeOffset LockedByAdminUntil = DateTimeOffset.MaxValue;

    [Obsolete("EF Use Only")]
#pragma warning disable CS8618 // EF Core sets the properties when it materialises the entity
    public FinanceTrackerUser()
    {
    }
#pragma warning restore CS8618

    public FinanceTrackerUser(string firstName, string lastName, string email, string username) : base(username)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
    }

    public FinanceTrackerUser(string firstName, string lastName, string email, string username, string oidcSubject) :
        this(firstName, lastName, email, username) => OpenIdConnectSubject = oidcSubject;

    public string FirstName { get; private set; }
    public string LastName { get; private set; }

    public string? OpenIdConnectSubject { get; private set; }
    public void SetOidcSubject(string subject) => OpenIdConnectSubject = subject;

    /// <summary>
    /// Whether an admin has locked them, so they can't sign in and their banks aren't synced.
    /// </summary>
    public bool IsLockedByAdmin => LockoutEnd == LockedByAdminUntil;

    /// <summary>
    /// Locks them until an admin unlocks them, see <see cref="LockedByAdminUntil"/>.
    /// </summary>
    public void LockByAdmin()
    {
        LockoutEnabled = true;
        LockoutEnd = LockedByAdminUntil;
    }

    /// <summary>
    /// Lifts an admin's lock, and any lockout from wrong passwords with it.
    /// </summary>
    public void Unlock()
    {
        LockoutEnd = null;
        AccessFailedCount = 0;
    }
}
