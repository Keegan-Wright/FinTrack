using Bogus;
using FinanceTracker.Domain.Identity;

namespace FinanceTracker.Tests.Unit.Shared.Fakers;

public sealed class FinanceTrackerUserFaker : Faker<FinanceTrackerUser>
{
    public FinanceTrackerUserFaker(string? username = null) =>
        CustomInstantiator(f =>
        {
            string firstName = f.Name.FirstName();
            string lastName = f.Name.LastName();
            return new FinanceTrackerUser(firstName, lastName, f.Internet.Email(firstName, lastName, uniqueSuffix: f.UniqueIndex.ToString()),
                username ?? f.Internet.UserName(firstName, lastName) + f.UniqueIndex);
        });
}
