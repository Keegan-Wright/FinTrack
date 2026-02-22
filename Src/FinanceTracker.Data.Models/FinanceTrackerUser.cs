using Microsoft.AspNetCore.Identity;

namespace FinanceTracker.Data.Models;

public class FinanceTrackerUser : IdentityUser<Guid>
{
    public string FirstName { get; set; }
    public string LastName { get; set; }

    public ICollection<OpenBankingProvider> Providers { get; set; }
    public ICollection<CustomClassification> CustomClassifications { get; set; }
    public ICollection<HouseholdMember> HouseholdMembers { get; set; }
    public ICollection<Debt> Debts { get; set; }
    public ICollection<BudgetCategory> BudgetCategories { get; set; }
    public ICollection<OpenBankingAccessToken> OpenBankingAccessTokens { get; set; }
}
