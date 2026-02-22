using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Data.Models;

public class BaseEntity
{
    public Guid Id { get; set; }
    public DateTime Created { get; set; }
    public DateTime? Updated { get; set; }

    [Timestamp]
    public uint RowVersion { get; set; }
}
