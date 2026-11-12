namespace FinanceTracker.Domain.Sharing;

/// <summary>
/// Why a <see cref="Share"/> exists. An item can be shared both ways at once, a row for each.
/// </summary>
public enum ShareReason
{
    /// <summary>
    /// Its owner chose to share it on the Sharing page. It shows in the other person's lists, and they can use it, or
    /// change it too as its mode allows.
    /// </summary>
    Chosen = 0,

    /// <summary>
    /// Its owner put it on the other person's transaction, so they see its name there. Read only, and nowhere else.
    /// </summary>
    OnTransaction = 1
}
