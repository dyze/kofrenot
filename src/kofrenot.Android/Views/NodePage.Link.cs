using Avalonia.Interactivity;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;

namespace Kofrenot.Phone.Views;

/// <summary>
/// + Link, as the Link box on Windows: a box typed after the node's name (<see cref="DetailsLink"/>), with its labels
/// and proposals (<see cref="ChipBox"/>); what Enter does said under it. Enter shows the creation screen of the link,
/// a new one or, its sentence typed again, the one there updated (its qualifiers); × hides the box.
/// </summary>
public sealed partial class NodePage
{
    private void AttachLinkBox()
    {
        LinkBox.GraphOf = () => Vault?.Graph;
        LinkBox.PlanOf = () => Link() is { Request: { } request } link && Vault is { } vault
            ? new AddPlanner(vault.Graph).Plan(request, link.Choices)
            : null;
        LinkBox.Log = _shell.Services.Log;
        LinkBox.Submit = AddLink;
        LinkBox.Changed += (_, _) => ShowLinkPreview();
    }

    private DetailsLink? Link() =>
        Vault is { } vault && Shown is { } node ? DetailsLink.Of(vault.Graph, node, LinkBox.Input.Typed) : null;

    private void AddLink_Click(object? sender, RoutedEventArgs e)
    {
        _shell.Services.Log.Info("Action", "Link box shown");
        LinkBox.Reset(fixedLeft: Shown?.Name);
        LinkPanel.IsVisible = true;
        AddLinkButton.IsVisible = false;
        ShowLinkPreview();
        LinkBox.FocusText();
    }

    private void CloseLink_Click(object? sender, RoutedEventArgs e) => HideLinkBox();

    private void HideLinkBox()
    {
        LinkBox.Reset();
        LinkPanel.IsVisible = false;
        AddLinkButton.IsVisible = !IsReadOnly;
    }

    private void ShowLinkPreview() => LinkPreview.Text = Link()?.Preview;

    // Enter: the link's creation screen, as Windows; the box hidden once it is added.
    private bool AddLink()
    {
        if (Link() is not { CanAdd: true, Request: { } request } link || Vault is not { } vault)
        {
            return false;
        }

        _shell.Services.Log.Info("Action", "Creation screen shown for a link from the details");
        _shell.Push(new CreationPage(_shell, new CreationForm(vault.Graph, request, link.Choices), added: HideLinkBox));
        return true;
    }
}
