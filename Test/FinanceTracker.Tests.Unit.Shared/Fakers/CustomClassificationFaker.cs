using Bogus;
using FinanceTracker.Domain.Classifications;

namespace FinanceTracker.Tests.Unit.Shared.Fakers;

public sealed class CustomClassificationFaker : Faker<CustomClassification>
{
    // Tags are unique per user
    public CustomClassificationFaker(Guid userId) =>
        CustomInstantiator(f => new CustomClassification(userId, $"{f.Commerce.Department()}-{f.UniqueIndex}"));
}
