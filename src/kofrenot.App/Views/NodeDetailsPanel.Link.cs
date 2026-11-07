using System.Windows;
using System.Windows.Input;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>
/// The link box, shown on demand (+ Link, Ctrl+K) in place of + Link, beside the links' header: links the node shown to
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

    /// <summary>Shows the link box in place of + Link, ready to type (+ Link, Ctrl+K).</summary>
    /// <remarks>Laid out first: a box just shown cannot take the keyboard until then.</remarks>
    public void StartLink()
    {
        AddLinkButton.Visibility = Visibility.Collapsed;
        LinkBox.Visibility = Visibility.Visible;
        LinkBox.Reset(fixedLeft: Browser.Details?.Node.Name);
        UpdateLayout();
        LinkBox.FocusText();
    }

    private void HideLinkBox()
    {
        LinkBox.Reset();
        LinkBox.Visibility = Visibility.Collapsed;
        AddLinkButton.Visibility = Visibility.Visible;

        // The reset showed the empty box's hint while the box was still shown: hidden with it.
        ShowLinkPreview();
    }

    private void ShowLinkPreview()
    {
        LinkPreviewText.Text = Link()?.Preview ?? string.Empty;
        LinkPreviewText.Visibility = LinkPreviewText.Text.Length > 0 && LinkBox.Visibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// While the link box has the focus: Enter adds, Tab accepts the proposal, Esc hides it, else clears the box,
    /// then hides it; while several proposals are listed, Up/Down select one and Enter accepts it too; other keys
    /// are typed (Right and Backspace by the box).
    /// </summary>
    private bool HandleLinkKey(Key key)
    {
        switch (key)
        {
            case Key.Up or Key.Down when LinkBox.HasProposalList:
                LinkBox.MoveProposal(key == Key.Up ? -1 : 1);
                return true;
            case Key.Tab:
            case Key.Enter when LinkBox.HasProposalList:
                LinkBox.AcceptProposal();
                return true;
            case Key.Enter:
                // All of it as labels first, as in the input box, drawn before the creation screen opens.
                if (Link() is { CanAdd: true, Request: { } request } link)
                {
                    LinkBox.LabelAll();
                    if (AddLink?.Invoke(request, link.Choices) == true)
                    {
                        HideLinkBox();
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
                    HideLinkBox();
                    Focus();
                }

                return true;
            default:
                return false;
        }
    }
}
