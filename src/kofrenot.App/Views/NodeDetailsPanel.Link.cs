using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>The Link box: links the node shown to another one, typed as in the input box after its name (<see cref="DetailsLink"/>).</summary>
public partial class NodeDetailsPanel
{
    /// <summary>Shows the creation screen for a link of the node shown, then adds it; false when cancelled or not written.</summary>
    public Func<LinkRequest, AddChoices, bool>? AddLink { get; set; }

    private DetailsLink? Link() =>
        Browser is { Graph: { } graph, Details: { } details } ? DetailsLink.Of(graph, details.Node, LinkBox.Text) : null;

    private void LinkBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        LinkPlaceholder.Visibility = LinkBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var link = LinkBox.Text.Length == 0 ? null : Link();
        LinkPreviewText.Text = link?.Preview ?? string.Empty;
        LinkPreviewText.Visibility = link is null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>While the Link box has the focus: Enter adds, Esc clears it (then leaves it); other keys are typed.</summary>
    private bool HandleLinkKey(Key key)
    {
        switch (key)
        {
            case Key.Enter:
                if (Link() is { CanAdd: true, Request: { } request } link && AddLink?.Invoke(request, link.Choices) == true)
                {
                    LinkBox.Clear();
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
                    Focus();
                }

                return true;
            default:
                return false;
        }
    }
}
