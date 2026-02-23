using Microsoft.AspNetCore.Identity;

namespace FinanceTracker.Data.Models;

public class FinanceTrackerUser : IdentityUser<Guid>
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }

    public ICollection<OpenBankingProvider>? Providers { get; set; }
    public ICollection<CustomClassification>? CustomClassifications { get; init; }
    public ICollection<HouseholdMember>? HouseholdMembers { get; init; }
    public ICollection<Debt>? Debts { get; init; }
    public ICollection<BudgetCategory>? BudgetCategories { get; init; }
    public ICollection<OpenBankingAccessToken>? OpenBankingAccessTokens { get; set; }
}
