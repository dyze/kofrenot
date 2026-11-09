using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;

namespace Kofrenot.Phone.Views;

/// <summary>
/// A box typed as the input box on Windows (<see cref="ChipInput"/>): its labels before the text, the
/// proposal after it, the list of proposals under it when there are several. The search box of Home,
/// the Link box of a node's details (its node a fixed first label). The box holds only the text after the labels.
/// </summary>
public sealed partial class ChipBox : UserControl
{
    // The labels take at most this part of the box, scrolled to their end: the text keeps the rest.
    private const double ChipsShare = 0.5;

    private string? _placeholder;

    public ChipBox()
    {
        InitializeComponent();

        // Backspace is seen before the box handles it: with no text left, it turns the last label back into text.
        // The proposal follows the caret: shown only while it is at the end.
        Box.AddHandler(KeyDownEvent, Box_KeyDown, RoutingStrategies.Tunnel);
        Box.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.CaretIndexProperty)
            {
                PlaceProposalLater();
            }
        };
        Box.LostFocus += (_, _) => ProposalList.IsOpen = false;
        Area.SizeChanged += (_, e) => ChipsScroller.MaxWidth = e.NewSize.Width * ChipsShare;

        // The last label in sight.
        HorizontalScroll.KeepRight(ChipsScroller);
    }

    /// <summary>What is typed: its labels and text.</summary>
    public ChipInput Input { get; private set; } = new();

    /// <summary>The open vault's graph; null when none is open (the box then reads nothing).</summary>
    public Func<KnowledgeGraph?>? GraphOf { get; set; }

    /// <summary>What the text adds, for the labels' pictures (the nodes it uses); null when it adds nothing.</summary>
    public Func<AddPlan?>? PlanOf { get; set; }

    /// <summary>Enter with no proposal to accept: true when the owner did something with it.</summary>
    public Func<bool>? Submit { get; set; }

    public ILog? Log { get; set; }

    /// <summary>The text shown while nothing is typed.</summary>
    public string? Placeholder
    {
        get => _placeholder;
        set
        {
            _placeholder = value;
            Box.PlaceholderText = Input.IsEmpty ? value : null;
        }
    }

    /// <summary>The labels or the text changed: by typing, a proposal accepted, a label undone, ×.</summary>
    public event EventHandler? Changed;

    /// <summary>Empty again; with <paramref name="fixedLeft"/>, that node's name the fixed first label, not shown.</summary>
    public void Reset(string? fixedLeft = null)
    {
        Input = new ChipInput(fixedLeft);
        SetText(string.Empty);
        Refresh();
    }

    /// <summary>All of it replaced by <paramref name="text"/>, read as typed (a query started from the list).</summary>
    public void Replace(string text)
    {
        Input.Replace(text);
        SetText(Input.Tail);
        RaiseChanged();
    }

    /// <summary>Before the creation screen: all of the text as labels.</summary>
    public void LabelAll()
    {
        if (GraphOf?.Invoke() is { } graph)
        {
            Input.LabelAll(graph);
            SetText(Input.Tail);
            Refresh();
        }
    }

    /// <summary>Labels and text gone, without telling <see cref="Changed"/>: the owner shows itself again.</summary>
    public void Clear()
    {
        Input.Clear();
        SetText(string.Empty);
        Refresh();
    }

    public void FocusText() => Box.Focus();

    /// <summary>The labels, the proposal and its list shown as they stand.</summary>
    public void Refresh()
    {
        Box.PlaceholderText = Input.IsEmpty ? _placeholder : null;
        ClearButton.IsVisible = !Input.IsEmpty;
        if (GraphOf?.Invoke() is not { } graph)
        {
            return;
        }

        var chips = Input.Chips(graph, PlanOf?.Invoke());
        Chips.Children.Clear();
        for (var index = 0; index < chips.Count; index++)
        {
            Chips.Children.Add(ChipView(chips[index], isLast: index == chips.Count - 1));
        }

        Chips.IsVisible = chips.Count > 0;
        ChipsScroller.MaxWidth = Area.Bounds.Width * ChipsShare;
        ShowProposals(graph);
    }

    private void RaiseChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
        Refresh();
    }

    // Avalonia tells of a change later, maybe several times: each reads the box as it is now, the same
    // text again changing nothing (ChipInput.Type), the text set here included.
    private void Box_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (GraphOf?.Invoke() is not { } graph)
        {
            return;
        }

        if (Input.Type(graph, Box.Text ?? string.Empty))
        {
            SetText(Input.Tail);
        }

        RaiseChanged();
    }

    private void Box_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Back && Box.CaretIndex == 0 && Box.SelectionStart == Box.SelectionEnd)
        {
            e.Handled = UnlabelLast();
        }
        else if (e.Key == Key.Enter)
        {
            // As on Windows: the proposal becomes a label (Tab there), or else the owner's own (Enter there).
            e.Handled = AcceptProposal() || (Submit?.Invoke() ?? false);
        }
    }

    private void ProposalText_Tapped(object? sender, TappedEventArgs e) => AcceptProposal();

    /// <summary>A tap on a proposal, or Enter (Tab on Windows): it becomes a label. False when there is none.</summary>
    private bool AcceptProposal()
    {
        if (GraphOf?.Invoke() is not { } graph || !Input.Accept(graph))
        {
            return false;
        }

        Log?.Debug("Input", "Proposal accepted");
        SetText(Input.Tail);
        RaiseChanged();
        Box.Focus();
        return true;
    }

    /// <summary>Backspace with no text, or a tap on the last label: it becomes text again.</summary>
    private bool UnlabelLast()
    {
        if (!Input.Backspace())
        {
            return false;
        }

        SetText(Input.Tail);
        RaiseChanged();
        return true;
    }

    // ×: its labels and text gone at once, the box ready for another.
    private void Clear_Click(object? sender, RoutedEventArgs e)
    {
        Log?.Info("Action", "Input box cleared");
        Input.Clear();
        SetText(string.Empty);
        RaiseChanged();
        Box.Focus();
    }

    // Once the box has laid out its new text.
    private void PlaceProposalLater() => Dispatcher.UIThread.Post(PlaceProposal, DispatcherPriority.Loaded);

    // The proposal right after the text, on its line, while the caret is at its end and the end is in sight;
    // the list under the box at the same times, when there are several.
    private void PlaceProposal()
    {
        var text = Box.Text ?? string.Empty;
        var atEnd = Box.CaretIndex == text.Length && Box.SelectionStart == Box.SelectionEnd;
        var presenter = Box.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        var end = presenter?.TranslatePoint(new Point(presenter.TextLayout.HitTestTextPosition(text.Length).Right, 0), Area);
        ProposalText.IsVisible = !string.IsNullOrEmpty(ProposalText.Text) && text.Length > 0 && atEnd
            && end is { X: var x } && x < Area.Bounds.Width;
        if (end is { } point)
        {
            ProposalText.Margin = new Thickness(point.X, point.Y, 0, 0);
        }

        ProposalList.IsOpen = ProposalItems.Children.Count > 1 && atEnd && Box.IsFocused;
    }

    // A label as on Windows: a node's icon and colour, or new (dashed, ＋); the link type in the accent.
    private Control ChipView(InputChip chip, bool isLast)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        if (chip.IsNew)
        {
            content.Children.Add(ChipIcon("add", NodeLook.Brush(NodeColor.Green)));
        }
        else if (chip.Visual is { } visual)
        {
            content.Children.Add(ChipIcon(IconPaths.NameOf(visual), NodeLook.Brush(visual.Color)));
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
        else if (chip.IsQualifier)
        {
            border.Classes.Add("qualifier");
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

    // A chip's icon, the size of its text, in a colour or the text's own.
    private static PathIcon ChipIcon(string name, IBrush? color)
    {
        var icon = new PathIcon { Data = PhoneTheme.Icon(name), Width = 14, Height = 14, VerticalAlignment = VerticalAlignment.Center };
        if (color is not null)
        {
            icon.Foreground = color;
        }

        return icon;
    }

    // Sets the text after the labels without reading it as typed, the caret at its end.
    private void SetText(string text)
    {
        Box.Text = text;
        Box.CaretIndex = text.Length;
    }
}
