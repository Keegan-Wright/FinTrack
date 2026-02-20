using FinanceTracker.Data.Models.Utility;

namespace FinanceTracker.Data.Models;

public class CustomClassification : BaseEntity
{
    
    [Encrypt]
    public required string Tag { get; set; }
}