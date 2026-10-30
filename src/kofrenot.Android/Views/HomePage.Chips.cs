using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;

namespace Kofrenot.Phone.Views;

/// <summary>
/// The labels of the search box (<see cref="ChipInput"/>), before its text, and the proposal at its
/// end, as the input box on Windows. The box holds only the text after the labels.
/// </summary>
public sealed partial class HomePage
{
    private readonly ChipInput _chips = new();
    private string? _placeholder;

    // Backspace is seen before the box handles it: with no text left, it turns the last label back into text.
    // The proposal follows the caret: shown only while it is at the end.
    private void WatchSearchBox()
    {
        _placeholder = SearchBox.PlaceholderText;
        ProposalText.FontSize = SearchBox.FontSize;
        SearchBox.AddHandler(KeyDownEvent, SearchBox_KeyDown, RoutingStrategies.Tunnel);
        SearchBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.CaretIndexProperty)
            {
                PlaceProposalLater();
            }
        };
    }

    // Avalonia tells of a change later, maybe several times: each reads the box as it is now, the same
    // text again changing nothing (ChipInput.Type), the text set here included.
    private void Search_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (Vault is not { } vault)
        {
            return;
        }

        if (_chips.Type(vault.Graph, SearchBox.Text ?? string.Empty))
        {
            SetSearchText(_chips.Tail);
        }

        ShowList(vault);
    }

    private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Back && SearchBox.CaretIndex == 0 && SearchBox.SelectionStart == SearchBox.SelectionEnd)
        {
            e.Handled = UnlabelLast();
        }
        else if (e.Key == Key.Enter)
        {
            // As on Windows: the proposal becomes a label (Tab there), or else what the text adds shows its creation screen.
            e.Handled = AcceptProposal() || ShowCreation();
        }
    }

    private void Proposal_Tapped(object? sender, TappedEventArgs e) => AcceptProposal();

    /// <summary>A tap on the proposal, or Enter (Tab on Windows): it becomes a label. False when there is none.</summary>
    private bool AcceptProposal()
    {
        if (Vault is not { } vault || !_chips.Accept(vault.Graph))
        {
            return false;
        }

        _shell.Services.Log.Debug("Input", "Proposal accepted");
        SetSearchText(_chips.Tail);
        ShowList(vault);
        SearchBox.Focus();
        return true;
    }

    /// <summary>Backspace with no text, or a tap on the last label: it becomes text again.</summary>
    private bool UnlabelLast()
    {
        if (Vault is not { } vault || !_chips.Backspace())
        {
            return false;
        }

        SetSearchText(_chips.Tail);
        ShowList(vault);
        return true;
    }

    /// <summary>Before the creation screen: all of the text as labels.</summary>
    private void LabelAll(KnowledgeGraph graph)
    {
        _chips.LabelAll(graph.Types);
        SetSearchText(_chips.Tail);
        ShowChips(graph);
    }

    private void ClearSearch()
    {
        _chips.Clear();
        SetSearchText(string.Empty);
    }

    private void ShowChips(KnowledgeGraph graph)
    {
        var chips = _chips.Chips(graph, _adding?.Plan);
        Chips.Children.Clear();
        for (var index = 0; index < chips.Count; index++)
        {
            Chips.Children.Add(ChipView(chips[index], isLast: index == chips.Count - 1));
        }

        Chips.IsVisible = chips.Count > 0;
        SearchBox.PlaceholderText = _chips.IsEmpty ? _placeholder : null;
        ProposalText.Text = _chips.Proposal(graph)?.Rest;
        PlaceProposalLater();
    }

    // Once the box has laid out its new text.
    private void PlaceProposalLater() => Dispatcher.UIThread.Post(PlaceProposal, DispatcherPriority.Loaded);

    // The proposal right after the text, on its line, while the caret is at its end and the end is in sight.
    private void PlaceProposal()
    {
        var text = SearchBox.Text ?? string.Empty;
        var presenter = SearchBox.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        var end = presenter?.TranslatePoint(new Point(presenter.TextLayout.HitTestTextPosition(text.Length).Right, 0), SearchArea);
        ProposalText.IsVisible = !string.IsNullOrEmpty(ProposalText.Text) && text.Length > 0
            && SearchBox.CaretIndex == text.Length && SearchBox.SelectionStart == SearchBox.SelectionEnd
            && end is { X: var x } && x < SearchArea.Bounds.Width;
        if (end is { } point)
        {
            ProposalText.Margin = new Thickness(point.X, point.Y, 0, 0);
        }
    }

    // A label as on Windows: a node's glyph and colour, or new (dashed, ＋); the link type in the accent.
    private Control ChipView(InputChip chip, bool isLast)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        if (chip.IsNew)
        {
            content.Children.Add(new TextBlock { Text = "＋", Foreground = NodeLook.Brush(NodeColor.Green), VerticalAlignment = VerticalAlignment.Center });
        }
        else if (chip.Visual is { } visual)
        {
            var glyph = new TextBlock { Text = NodeLook.GlyphOf(visual), VerticalAlignment = VerticalAlignment.Center };
            if (NodeLook.Brush(visual.Color) is { } color)
            {
                glyph.Foreground = color;
            }

            content.Children.Add(glyph);
        }

        content.Children.Add(new TextBlock { Text = chip.Text, VerticalAlignment = VerticalAlignment.Center });
        if (chip.HomonymMark.Length > 0)
        {
            content.Children.Add(new TextBlock { Text = chip.HomonymMark, Classes = { "caption" }, VerticalAlignment = VerticalAlignment.Center });
        }

        var border = new Border { Child = content, Classes = { "chip" } };
        if (chip.IsLinkType)
        {
            border.Classes.Add("link");
        }

        Control view = border;
        if (chip.IsNew)
        {
            border.Classes.Add("new");
            view = new Panel { Children = { new Rectangle { Classes = { "newOutline" } }, border } };
        }

        if (isLast)
        {
            view.Tapped += (_, _) => UnlabelLast();
        }

        return view;
    }

    // Sets the text after the labels without reading it as typed, the caret at its end.
    private void SetSearchText(string text)
    {
        SearchBox.Text = text;
        SearchBox.CaretIndex = text.Length;
    }
}
