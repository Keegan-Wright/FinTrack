namespace FinanceTracker.Contracts.Bills;

/// <summary>
/// A suggested subscription's payments are already covered by the user's bill <see cref="BillName"/>, so making it a
/// bill would count them twice.
/// </summary>
public sealed class AlreadyABillException : Exception
{
    /// <summary>
    /// Where the API's 409 for it names the bill, alongside the problem's message.
    /// </summary>
    public const string BillNameField = "billName";

    /// <param name="billName">The bill that already covers them.</param>
    public AlreadyABillException(string billName) : base($"Your bill {billName} already covers these payments") =>
        BillName = billName;

    /// <summary>
    /// The bill that already covers them.
    /// </summary>
    public string BillName { get; }
}
