using FinanceTracker.Domain.Utility;

namespace FinanceTracker.Domain;

public class CustomClassification : BaseEntity
{
    public CustomClassification(string tag) => Tag = tag;

    [Encrypt] public string Tag { get; private set; }
}
