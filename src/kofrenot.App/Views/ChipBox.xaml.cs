using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>
/// A box typed as the input box (<see cref="ChipInput"/>): the labels over its start, the proposal in grey after
/// its text. The box holds only the text after the labels. Right at the end of the text accepts the proposal,
/// Backspace at its start unlabels; its host handles the other keys (Tab: <see cref="AcceptProposal"/>, Esc).
/// </summary>
public partial class ChipBox : UserControl
{
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(ChipBox), new PropertyMetadata(string.Empty, (box, e) => ((ChipBox)box).PlaceholderText.Text = (string)e.NewValue));

    public static readonly DependencyProperty VerticalPaddingProperty = DependencyProperty.Register(
        nameof(VerticalPadding), typeof(double), typeof(ChipBox), new PropertyMetadata(6.0, (box, _) => ((ChipBox)box).PadText()));

    private const double TextPadding = 8;

    private bool _settingText;

    public ChipBox()
    {
        InitializeComponent();
    }

    /// <summary>Its labels, then the text after them.</summary>
    public ChipInput Input { get; private set; } = new();

    /// <summary>The graph its text is read in; nothing is labelled or proposed without one.</summary>
    public Func<KnowledgeGraph?>? GraphOf { get; set; }

    /// <summary>What the host plans to add, so a node's label pictures the homonym it uses (else the oldest).</summary>
    public Func<AddPlan?>? PlanOf { get; set; }

    /// <summary>Raised when its text or labels change, before the labels are drawn again.</summary>
    public event Action? Changed;

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public double VerticalPadding
    {
        get => (double)GetValue(VerticalPaddingProperty);
        set => SetValue(VerticalPaddingProperty, value);
    }

    public bool IsEmpty => Input.IsEmpty;

    /// <summary>Empties it; with a fixed first node from then on (the node of the details), or none.</summary>
    public void Reset(string? fixedLeft = null)
    {
        Input = new ChipInput(fixedLeft);
        Update();
    }

    /// <summary>Tab, or Right at the end of the text: the proposal becomes a label. False when there is none.</summary>
    public bool AcceptProposal()
    {
        if (GraphOf?.Invoke() is not { } graph || !CaretAtEnd || !Input.Accept(graph))
        {
            return false;
        }

        Update();
        return true;
    }

    /// <summary>Esc: hides the proposal. False when there was none.</summary>
    public bool DismissProposal()
    {
        if (GraphOf?.Invoke() is not { } graph || !Input.Dismiss(graph))
        {
            return false;
        }

        ShowChips();
        return true;
    }

    /// <summary>Before the creation screen: all of the text as labels, drawn before the screen opens over them.</summary>
    public void LabelAll()
    {
        if (GraphOf?.Invoke() is { } graph)
        {
            Input.LabelAll(graph);
            Update();
            Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
        }
    }

    public void Clear()
    {
        Input.Clear();
        Update();
    }

    /// <summary>All of it as text, selected, so typing replaces it: labels included.</summary>
    public void UnlabelAndSelect()
    {
        Input.Unlabel();
        Update();
        Box.Focus();
        Box.SelectAll();
    }

    /// <summary>The keyboard to its text.</summary>
    public void FocusText() => Box.Focus();

    /// <summary>The labels and the proposal drawn again (the host's plan changed).</summary>
    public void ShowChips()
    {
        var graph = GraphOf?.Invoke();
        PlaceholderText.Visibility = Input.IsEmpty && graph is not null ? Visibility.Visible : Visibility.Collapsed;
        Chips.ItemsSource = graph is null ? null : Input.Chips(graph, PlanOf?.Invoke());
        ProposalText.Text = graph is null ? string.Empty : Input.Proposal(graph)?.Rest ?? string.Empty;
        PlaceProposal();
    }

    private void Box_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_settingText || GraphOf?.Invoke() is not { } graph)
        {
            return;
        }

        if (Input.Type(graph, Box.Text))
        {
            SetText(Input.Tail);
        }

        Changed?.Invoke();
        ShowChips();
    }

    private void Box_SelectionChanged(object sender, RoutedEventArgs e) => PlaceProposal();

    private void Box_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = e.Key switch
            {
                Key.Right => AcceptProposal(),
                Key.Back => UnlabelLast(),
                _ => false,
            };
        }
    }

    private void Chips_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        PadText();
        PlaceProposal();
    }

    private void PadText() =>
        Box.Padding = new Thickness(TextPadding + Chips.ActualWidth, VerticalPadding, TextPadding, VerticalPadding);

    /// <summary>Backspace at the start of the text, nothing selected: the last label becomes text again.</summary>
    private bool UnlabelLast()
    {
        if (Box.CaretIndex > 0 || Box.SelectionLength > 0 || !Input.Backspace())
        {
            return false;
        }

        Update();
        return true;
    }

    private void Update()
    {
        SetText(Input.Tail);
        Changed?.Invoke();
        ShowChips();
    }

    // The proposal right after the text, while the caret is at its end.
    private void PlaceProposal()
    {
        if (ProposalText.Text.Length == 0 || Box.Text.Length == 0 || !CaretAtEnd)
        {
            ProposalText.Visibility = Visibility.Collapsed;
            return;
        }

        Box.UpdateLayout();
        var end = Box.GetRectFromCharacterIndex(Box.Text.Length - 1, trailingEdge: true);
        ProposalText.Margin = new Thickness(end.Right, 0, 0, 0);
        ProposalText.Visibility = Visibility.Visible;
    }

    private bool CaretAtEnd => Box.CaretIndex == Box.Text.Length && Box.SelectionLength == 0;

    // Sets the text after the labels without reading it as typed, the caret at its end.
    private void SetText(string text)
    {
        _settingText = true;
        Box.Text = text;
        Box.CaretIndex = text.Length;
        _settingText = false;
    }
}
