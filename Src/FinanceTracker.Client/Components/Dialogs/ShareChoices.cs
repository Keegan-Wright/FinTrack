using FinanceTracker.Contracts.Sharing;

namespace FinanceTracker.Client.Components.Dialogs;

/// <summary>
/// Everything the user chose to share in a link from <see cref="ShareDialog"/>. Their accounts, and their other things
/// such as tags. Each part is only saved when it changed.
/// </summary>
/// <param name="Accounts">Every account to share and how.</param>
/// <param name="AccountsChanged">Whether the accounts differ from what was shared.</param>
/// <param name="Items">Every other thing to share and how.</param>
/// <param name="ItemsChanged">Whether the other things differ from what was shared.</param>
public sealed record ShareChoices(
    ChangeAccountShares Accounts,
    bool AccountsChanged,
    ChangeItemShares Items,
    bool ItemsChanged);
