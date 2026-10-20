using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>
/// The link row, shown on demand (+ Link, Ctrl+K) at the top of the links: links the node shown to
/// another one, typed as in the input box after its name (<see cref="DetailsLink"/>).
/// </summary>
public partial class NodeDetailsPanel
{
    /// <summary>Shows the creation screen for a link of the node shown, then adds it; false when cancelled or not written.</summary>
    public Func<LinkRequest, AddChoices, bool>? AddLink { get; set; }

    private DetailsLink? Link() =>
        Browser is { Graph: { } graph, Details: { } details } ? DetailsLink.Of(graph, details.Node, LinkBox.Text) : null;

    private void AddLinkButton_Click(object sender, RoutedEventArgs e) => StartLink();

    /// <summary>Shows the link row, ready to type (+ Link, Ctrl+K).</summary>
    public void StartLink()
    {
        LinkRow.Visibility = Visibility.Visible;
        ShowLinkPreview();
        LinkBox.Focus();
    }

    private void HideLinkRow()
    {
        LinkBox.Clear();
        LinkRow.Visibility = Visibility.Collapsed;
    }

    private void LinkBox_TextChanged(object sender, TextChangedEventArgs e) => ShowLinkPreview();

    private void ShowLinkPreview()
    {
        LinkPlaceholder.Visibility = LinkBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        LinkPreviewText.Text = Link()?.Preview ?? string.Empty;
    }

    /// <summary>While the link row has the focus: Enter adds, Esc clears it, then hides it; other keys are typed.</summary>
    private bool HandleLinkKey(Key key)
    {
        switch (key)
        {
            case Key.Enter:
                if (Link() is { CanAdd: true, Request: { } request } link && AddLink?.Invoke(request, link.Choices) == true)
                {
                    HideLinkRow();
                    Focus();
                    Show();
                }

                return true;
            case Key.Escape:
                if (LinkBox.Text.Length > 0)
                {
                    LinkBox.Clear();
                }
                else
                {
                    HideLinkRow();
                    Focus();
                }

                return true;
            default:
                return false;
        }
    }
}
