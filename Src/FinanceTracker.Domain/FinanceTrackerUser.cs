using Microsoft.AspNetCore.Identity;

namespace FinanceTracker.Domain;

public class FinanceTrackerUser : IdentityUser<Guid>
{
    [Obsolete("EF Use Only")]
    public FinanceTrackerUser()
    {

    }

    public FinanceTrackerUser(string firstName, string lastName, string email, string username)
    {
        FirstName = firstName;
        LastName = lastName;
    }

    public FinanceTrackerUser(string firstName, string lastName, string email, string username, string oidcSubject) : this(firstName, lastName, email, username)
    {
        OpenIdConnectSubject = oidcSubject;
    }

    public string FirstName { get; init; }
    public string LastName { get; init; }

    public string? OpenIdConnectSubject { get; private set; }
    public ICollection<OpenBankingProvider>? Providers { get; set; }
    public ICollection<CustomClassification>? CustomClassifications { get; init; }
    public ICollection<HouseholdMember>? HouseholdMembers { get; init; }
    public ICollection<Debt>? Debts { get; init; }
    public ICollection<BudgetCategory>? BudgetCategories { get; init; }
    public ICollection<OpenBankingAccessToken>? OpenBankingAccessTokens { get; set; }


    public void SetOidcSubject(string subject)
    {
        OpenIdConnectSubject = subject;
    }
}
