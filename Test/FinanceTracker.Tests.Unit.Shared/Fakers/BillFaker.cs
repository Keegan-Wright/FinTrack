using Bogus;
using FinanceTracker.Domain.Bills;

namespace FinanceTracker.Tests.Unit.Shared.Fakers;

/// <summary>
/// A monthly bill the user added, paid from the account and starting this month.
/// </summary>
public sealed class BillFaker : Faker<Bill>
{
    public BillFaker(Guid userId, Guid accountId) =>
        CustomInstantiator(f => new Bill(userId, accountId, f.Company.CompanyName(), f.Company.CompanyName(),
            BillFrequency.Monthly, f.Finance.Amount(5, 100), DateOnly.FromDateTime(DateTime.UtcNow),
            BillSource.Manual));
}
