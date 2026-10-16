using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Domain;

public class BaseEntity
{
    public Guid Id { get; private set; }
    public DateTime Created { get; private set; }
    public DateTime? Updated { get; private set; }

    [Timestamp] public uint RowVersion { get; private set; }
}
