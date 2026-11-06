using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceTracker.Tests.Unit.Client;

/// <summary>
/// Clicks through a row's ⋮ menu, and the confirmation a delete asks for, the way a user would.
/// </summary>
public static class RowMenu
{
    /// <summary>
    /// Opens the menu labelled <c>Actions for {name}</c> and clicks <paramref name="item"/> in it.
    /// </summary>
    public static async Task ChooseAsync<TComponent>(IRenderedComponent<TComponent> cut,
        IRenderedComponent<MudPopoverProvider> popovers, string name, string item)
        where TComponent : IComponent =>
        await await StartAsync(cut, popovers, name, item);

    /// <summary>
    /// Opens the menu labelled <c>Actions for {name}</c> and clicks <paramref name="item"/> in it, without waiting
    /// for what the click does, for an item that asks first.
    /// </summary>
    /// <returns>What the click does, to await once the dialog it opens has been answered.</returns>
    public static async Task<Task> StartAsync<TComponent>(IRenderedComponent<TComponent> cut,
        IRenderedComponent<MudPopoverProvider> popovers, string name, string item)
        where TComponent : IComponent
    {
        await cut.Find($"button[aria-label='Actions for {name}']").ClickAsync(new());
        IElement menuItem = popovers.WaitForElements(".mud-popover-open .mud-menu-item")
            .Single(x => x.TextContent.Trim() == item);
        return menuItem.ClickAsync(new());
    }

    /// <summary>
    /// The items in the menu labelled <c>Actions for {name}</c>, and whether each can be clicked.
    /// </summary>
    public static async Task<IReadOnlyList<(string Text, bool Disabled)>> ItemsAsync<TComponent>(
        IRenderedComponent<TComponent> cut, IRenderedComponent<MudPopoverProvider> popovers, string name)
        where TComponent : IComponent
    {
        await cut.Find($"button[aria-label='Actions for {name}']").ClickAsync(new());
        return popovers.WaitForElements(".mud-popover-open .mud-menu-item")
            .Select(static x => (x.TextContent.Trim(), IsDisabled(x)))
            .ToList();
    }

    private static bool IsDisabled(IElement item) =>
        item.ClassList.Contains("mud-disabled") || item.GetAttribute("aria-disabled") == "true" ||
        item.HasAttribute("disabled");

    /// <summary>
    /// Clicks <paramref name="button"/> in the message box asking to confirm.
    /// </summary>
    public static async Task ConfirmAsync(IRenderedComponent<MudDialogProvider> dialogs, string button)
    {
        IElement yes = dialogs.WaitForElement(".mud-dialog .mud-dialog-actions")
            .QuerySelectorAll("button")
            .Single(x => x.TextContent.Trim() == button);
        await yes.ClickAsync(new());
    }
}
