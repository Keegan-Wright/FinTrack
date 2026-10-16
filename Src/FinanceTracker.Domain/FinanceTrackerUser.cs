using Microsoft.AspNetCore.Identity;

namespace FinanceTracker.Domain;

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
    public ICollection<OpenBankingProvider>? Providers { get; private set; }
    public ICollection<CustomClassification>? CustomClassifications { get; private set; }
    public ICollection<HouseholdMember>? HouseholdMembers { get; private set; }
    public ICollection<Debt>? Debts { get; private set; }
    public ICollection<BudgetCategory>? BudgetCategories { get; private set; }
    public ICollection<OpenBankingAccessToken>? OpenBankingAccessTokens { get; private set; }


    public void SetOidcSubject(string subject) => OpenIdConnectSubject = subject;
}
