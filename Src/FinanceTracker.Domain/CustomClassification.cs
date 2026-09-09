using FinanceTracker.Domain.Utility;

namespace FinanceTracker.Domain;

public class CustomClassification : BaseEntity
{
    [Encrypt]
    public required string Tag { get; init; }
}
