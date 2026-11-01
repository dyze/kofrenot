using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;

namespace Kofrenot.UI.Views;

/// <summary>
/// The labels of the input box (<see cref="ChipInput"/>), over its start, and the proposal in grey
/// after its text. The box holds only the text after the labels.
/// </summary>
public partial class MainWindow
{
    private const double InputPadding = 8;

    private readonly ChipInput _chips = new();
    private bool _settingInput;

    private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_settingInput || _session.Current is not { } vault)
        {
            return;
        }

        if (_chips.Type(vault.Graph, InputBox.Text))
        {
            SetInputText(_chips.Tail);
        }

        ShowInput();
    }

    private void InputBox_SelectionChanged(object sender, RoutedEventArgs e) => PlaceProposal();

    private void InputChips_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        InputBox.Padding = new Thickness(InputPadding + InputChips.ActualWidth, 6, InputPadding, 6);
        PlaceProposal();
    }

    /// <summary>Tab, or Right at the end of the text: the proposal becomes a label. False when there is none.</summary>
    private bool AcceptProposal()
    {
        if (_session.Current is not { } vault || !CaretAtEnd || !_chips.Accept(vault.Graph))
        {
            return false;
        }

        _log.Debug("Input", "Proposal accepted");
        SetInputText(_chips.Tail);
        ShowInput();
        return true;
    }

    /// <summary>Backspace at the start of the text, nothing selected: the last label becomes text again.</summary>
    private bool UnlabelLast()
    {
        if (InputBox.CaretIndex > 0 || InputBox.SelectionLength > 0 || !_chips.Backspace())
        {
            return false;
        }

        SetInputText(_chips.Tail);
        ShowInput();
        return true;
    }

    /// <summary>Esc: hides the proposal. False when there was none.</summary>
    private bool DismissProposal()
    {
        if (_session.Current is not { } vault || !_chips.Dismiss(vault.Graph))
        {
            return false;
        }

        ShowChips(vault.Graph);
        return true;
    }

    /// <summary>Before the creation screen: all of the text as labels, drawn before the screen opens over them.</summary>
    private void LabelAll()
    {
        if (_session.Current is not { } vault)
        {
            return;
        }

        _chips.LabelAll(vault.Graph.Types);
        SetInputText(_chips.Tail);
        ShowInput();
        Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
    }

    private void ClearInput()
    {
        _chips.Clear();
        SetInputText(string.Empty);
        ShowInput();
    }

    private void ShowChips(KnowledgeGraph graph)
    {
        InputChips.ItemsSource = _chips.Chips(graph, _input.Plan);
        InputProposal.Text = _chips.Proposal(graph)?.Rest ?? string.Empty;
        PlaceProposal();
    }

    // The proposal right after the text, while the caret is at its end.
    private void PlaceProposal()
    {
        if (InputProposal.Text.Length == 0 || InputBox.Text.Length == 0 || !CaretAtEnd)
        {
            InputProposal.Visibility = Visibility.Collapsed;
            return;
        }

        InputBox.UpdateLayout();
        var end = InputBox.GetRectFromCharacterIndex(InputBox.Text.Length - 1, trailingEdge: true);
        InputProposal.Margin = new Thickness(end.Right, 0, 0, 0);
        InputProposal.Visibility = Visibility.Visible;
    }

    private bool CaretAtEnd => InputBox.CaretIndex == InputBox.Text.Length && InputBox.SelectionLength == 0;

    // Sets the text after the labels without reading it as typed, the caret at its end.
    private void SetInputText(string text)
    {
        _settingInput = true;
        InputBox.Text = text;
        InputBox.CaretIndex = text.Length;
        _settingInput = false;
    }
}
