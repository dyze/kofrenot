using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>A node type in the creation screen's list, with its icon.</summary>
public sealed record TypeChoice(NodeType Type, NodeVisual Visual);

/// <summary>
/// The creation screen, shown by <see cref="VaultSession.Create"/> from anywhere: a view of a
/// <see cref="CreationForm"/>, one step at a time, whose choices it passes on.
/// </summary>
public partial class CreationDialog : Window
{
    private readonly CreationForm _form;
    private readonly IReadOnlyList<TypeChoice> _typeChoices;
    private bool _showing;
    private bool _discardConfirmed;

    private CreationDialog(Window owner, CreationForm form)
    {
        InitializeComponent();
        Owner = owner;
        _form = form;

        TitleText.Text = form.Title;
        _typeChoices = form.Types.NodeTypes.Select(type => new TypeChoice(type, NodeVisual.OfType(type.Id))).ToList();
        TypeBox.ItemsSource = _typeChoices;
        LinkTypeBox.ItemsSource = form.Types.Verbs;
        ViewLinkBox.ItemsSource = form.Types.LinkTypes;
        ViewTypeBox.ItemsSource = form.Types.NodeTypes;
        ContentBox.Edited += ContentBox_Edited;
        Loaded += (_, _) => ShowStep();
    }

    /// <summary>Shows the screen: true when the user added (the form then holds the plan and the notes), false when cancelled.</summary>
    public static bool Ask(Window owner, CreationForm form) => new CreationDialog(owner, form).ShowDialog() == true;

    private Side? StepSide => _form.Step.Side;

    /// <summary>A new step: its content, then the focus on its first field (the type, or the link type).</summary>
    private void ShowStep()
    {
        if (StepSide is { } side)
        {
            ContentBox.Text = _form.ContentOf(side);
            RootBox.Text = _form.ViewOf(side).Root?.Name ?? string.Empty;
        }

        ShowForm();
        FocusStep();
    }

    private void FocusStep()
    {
        if (StepSide is null)
        {
            LinkTypeBox.Focus();
        }
        else
        {
            FocusType();
        }
    }

    // On the chosen type itself, so Up/Down move from it at once; its item exists once laid out.
    private void FocusType()
    {
        TypeBox.UpdateLayout();
        if (TypeBox.ItemContainerGenerator.ContainerFromItem(TypeBox.SelectedItem) is ListBoxItem item)
        {
            item.Focus();
        }
        else
        {
            TypeBox.Focus();
        }
    }

    private void ShowForm()
    {
        _showing = true;
        StepText.Text = _form.StepText;
        NodePanel.Visibility = StepSide is null ? Visibility.Collapsed : Visibility.Visible;
        LinkPanel.Visibility = StepSide is null ? Visibility.Visible : Visibility.Collapsed;
        if (StepSide is { } side)
        {
            ShowNode(side);
        }
        else if (_form.Plan is LinkPlan link)
        {
            ShowSide(link.Left, LeftName, LeftInfo);
            ShowSide(link.Right!, RightName, RightInfo);
            LinkTypeBox.SelectedItem = _form.Verb;
        }

        ShowStatus();
        BackButton.Visibility = _form.StepIndex > 0 ? Visibility.Visible : Visibility.Collapsed;
        NextButton.Content = _form.IsLastStep ? "Add" : "Next";
        NextButton.IsEnabled = !_reading && (_form.IsLastStep ? _form.CanAdd : _form.CanLeaveStep);
        _showing = false;
    }

    private void ShowNode(Side side)
    {
        NodeName.Text = _form.NameOf(side);
        TypeBox.SelectedItem = _typeChoices.First(choice => choice.Type == _form.TypeOf(side));
        ContentBox.Visibility = _form.IsNote(side) ? Visibility.Visible : Visibility.Collapsed;
        ShowFile(side);
        ShowView(side);

        // Only a node request can repeat a name: a link's new nodes have names no node has.
        WarningText.Text = _form.Warning ?? string.Empty;
        WarningText.Visibility = _form.Warning is null ? Visibility.Collapsed : Visibility.Visible;
        ExistingList.ItemsSource = _form.Existing;
    }

    private void ShowSide(PlannedSide side, TextBlock name, TextBlock info)
    {
        name.Text = _form.Label(side);
        info.Text = side.IsNew ? $"New node ({side.Type.Name})" : _form.Describe(side);
    }

    private void ShowStatus() => StatusText.Text = !_form.CanLeaveStep
        ? "Type the name of the node the tree starts from, and choose it."
        : _form.IsLastStep
            ? _form.Status
            : StepSide is { } side && _form.IsNote(side)
                ? "Tab: write the content. Enter (Ctrl+Enter in the content): next step."
                : StepSide is { } datasetSide && _form.IsDataset(datasetSide)
                    ? "Choose its file, or drop it here; it can also be imported later. Enter: next step."
                    : "Enter: next step.";

    private void TypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_showing || StepSide is not { } side || TypeBox.SelectedItem is not TypeChoice { Type: var type })
        {
            return;
        }

        _form.SetType(side, type);
        ShowForm();
    }

    private void LinkTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_showing && LinkTypeBox.SelectedItem is LinkVerb verb)
        {
            _form.Verb = verb;
            ShowForm();
        }
    }

    private void ContentBox_Edited()
    {
        if (StepSide is { } side)
        {
            _form.SetContent(side, ContentBox.Text);
        }
    }

    private void NextButton_Click(object sender, RoutedEventArgs e) => Next();

    /// <summary>The next step, or on the last one, Add; not while a chosen file is read.</summary>
    private void Next()
    {
        if (_reading)
        {
            return;
        }

        if (_form.Next())
        {
            ShowStep();
        }
        else if (_form.CanAdd)
        {
            DialogResult = true;
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_form.Back())
        {
            ShowStep();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            Next();
            e.Handled = true;
        }
    }

    /// <summary>Cancelling (Esc, Cancel, ×) asks first when a note's content or a dataset's file would be lost.</summary>
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (DialogResult == true || _discardConfirmed || !_form.HasContent)
        {
            return;
        }

        _discardConfirmed = MessageDialog.Confirm(this, "Discard the note content you typed, or the file you chose?", "Discard", "Keep editing");
        e.Cancel = !_discardConfirmed;
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindows.RemoveMinimizeMaximizeButtons(new WindowInteropHelper(this).Handle);
}
