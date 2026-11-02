namespace FinanceTracker.Client.Components.Transactions;

/// <summary>
/// One chip in a transaction's tags and links, see <see cref="TransactionChips"/>.
/// </summary>
/// <param name="Text">What it says, e.g. <c>Car loan</c>.</param>
/// <param name="Kind">What it stands for.</param>
/// <param name="Icon">Its icon, null for none.</param>
/// <param name="Href">Where it links, null when it doesn't.</param>
/// <param name="Title">Its tooltip, e.g. <c>Pays off Car loan, chosen by you</c>, null for none.</param>
public sealed record TransactionChip(string Text, TransactionChipKind Kind, string? Icon, string? Href, string? Title);
