using Microsoft.AspNetCore.Identity;

namespace FinanceTracker.Domain.Identity;

/// <summary>
/// Users of the application
/// </summary>
public sealed class FinanceTrackerUser : IdentityUser<Guid>
{
    [Obsolete("EF Use Only")]
    public FinanceTrackerUser()
    {
    }

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
}
