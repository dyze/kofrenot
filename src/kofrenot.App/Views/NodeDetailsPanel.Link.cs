using System.Windows;
using System.Windows.Input;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>
/// The link row, shown on demand (+ Link, Ctrl+K) at the top of the links: links the node shown to
/// another one, typed as in the input box after its name (<see cref="DetailsLink"/>), with its labels and proposals
/// (<see cref="ChipBox"/>): the node shown is its fixed first node.
/// </summary>
public partial class NodeDetailsPanel
{
    /// <summary>Shows the creation screen for a link of the node shown, then adds it; false when cancelled or not written.</summary>
    public Func<LinkRequest, AddChoices, bool>? AddLink { get; set; }

    private DetailsLink? Link() =>
        Browser is { Graph: { } graph, Details: { } details } ? DetailsLink.Of(graph, details.Node, LinkBox.Input.Typed) : null;

    private void AttachLinkBox()
    {
        LinkBox.GraphOf = () => Browser.Graph;
        LinkBox.Changed += ShowLinkPreview;
    }

    private void AddLinkButton_Click(object sender, RoutedEventArgs e) => StartLink();

    /// <summary>Shows the link row, ready to type (+ Link, Ctrl+K).</summary>
    public void StartLink()
    {
        LinkRow.Visibility = Visibility.Visible;
        LinkBox.Reset(fixedLeft: Browser.Details?.Node.Name);
        LinkBox.FocusText();
    }

    private void HideLinkRow()
    {
        LinkBox.Reset();
        LinkRow.Visibility = Visibility.Collapsed;
    }

    private void ShowLinkPreview() => LinkPreviewText.Text = Link()?.Preview ?? string.Empty;

    /// <summary>
    /// While the link row has the focus: Enter adds, Tab accepts the proposal, Esc hides it, else clears the row,
    /// then hides it; other keys are typed (Right and Backspace by the box).
    /// </summary>
    private bool HandleLinkKey(Key key)
    {
        switch (key)
        {
            case Key.Tab:
                LinkBox.AcceptProposal();
                return true;
            case Key.Enter:
                // All of it as labels first, as in the input box, drawn before the creation screen opens.
                if (Link() is { CanAdd: true, Request: { } request } link)
                {
                    LinkBox.LabelAll();
                    if (AddLink?.Invoke(request, link.Choices) == true)
                    {
                        HideLinkRow();
                        Focus();
                        Show();
                    }
                }

                return true;
            case Key.Escape:
                if (LinkBox.DismissProposal())
                {
                    return true;
                }

                if (!LinkBox.IsEmpty)
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
