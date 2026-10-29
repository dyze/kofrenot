using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
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
    private bool _settingSearch;

    // Backspace is seen before the box handles it: with no text left, it turns the last label back into text.
    private void WatchSearchBox()
    {
        _placeholder = SearchBox.PlaceholderText;
        SearchBox.AddHandler(KeyDownEvent, SearchBox_KeyDown, RoutingStrategies.Tunnel);
    }

    private void Search_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_settingSearch || Vault is not { } vault)
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
    }

    /// <summary>A tap on the proposal (Tab on Windows): it becomes a label.</summary>
    private void Proposal_Click(object? sender, RoutedEventArgs e)
    {
        if (Vault is { } vault && _chips.Accept(vault.Graph))
        {
            _shell.Services.Log.Debug("Input", "Proposal accepted");
            SetSearchText(_chips.Tail);
            ShowList(vault);
        }

        SearchBox.Focus();
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
        var proposal = _chips.Proposal(graph);
        ProposalText.Text = proposal?.Label;
        ProposalButton.IsVisible = proposal is not null;
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
        _settingSearch = true;
        SearchBox.Text = text;
        SearchBox.CaretIndex = text.Length;
        _settingSearch = false;
    }
}
