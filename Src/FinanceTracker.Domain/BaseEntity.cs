using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Domain;

public class BaseEntity
{
    public Guid Id { get; init; }
    public DateTime Created { get; set; }
    public DateTime? Updated { get; set; }

    [Timestamp]
    public uint RowVersion { get; set; }
}
