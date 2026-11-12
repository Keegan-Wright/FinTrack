using DomainShareMode = FinanceTracker.Domain.Sharing.ShareMode;
using ShareMode = FinanceTracker.Contracts.Sharing.ShareMode;

namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// Share modes between the contracts and the domain.
/// </summary>
internal static class ShareModeMapping
{
    public static ShareMode ToContract(DomainShareMode mode) => mode switch
    {
        DomainShareMode.ReadOnly => ShareMode.ReadOnly,
        DomainShareMode.AllowChanges => ShareMode.AllowChanges,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };

    public static DomainShareMode ToDomain(ShareMode mode) => mode switch
    {
        ShareMode.ReadOnly => DomainShareMode.ReadOnly,
        ShareMode.AllowChanges => DomainShareMode.AllowChanges,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };
}
