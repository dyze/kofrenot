using System.Windows;
using System.Windows.Interop;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Session;
using Kofrenot.Core.Views;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>
/// Edits a view: its name and what it shows, saved by the caller. Save waits for a complete
/// definition (a tree's root) and a name that can be given (<see cref="RenamePlanner"/>).
/// </summary>
public partial class ViewDialog : Window
{
    private readonly RenamePlanner _renames;
    private readonly Node _view;

    private ViewDialog(Window owner, KnowledgeGraph graph, Node view, ViewState loaded)
    {
        InitializeComponent();
        Owner = owner;
        _renames = new RenamePlanner(graph);
        _view = view;
        TitleText.Text = "Edit view";
        ConflictText.Visibility = loaded.IsConflict ? Visibility.Visible : Visibility.Collapsed;
        NameBox.Text = view.Name;
        NameBox.TextChanged += (_, _) => ShowSave();
        Editor.Setup(graph.Types, text => ViewDraft.FindRoots(graph, text));
        Editor.Load(loaded.Definition is { } definition ? ViewDraft.Of(definition, graph) : ViewDraft.Default);
        Editor.Changed += ShowSave;
        ShowSave();
        Loaded += (_, _) => Editor.FocusFirst();
    }

    private RenamePlan NamePlan => _renames.Plan(_view, NameBox.Text);

    /// <summary>Shows the dialog: what to save, or null when cancelled.</summary>
    public static ViewEdit? Ask(Window owner, KnowledgeGraph graph, Node view, ViewState loaded)
    {
        var dialog = new ViewDialog(owner, graph, view, loaded);
        return dialog.ShowDialog() == true && dialog.Editor.Draft.Definition is { } definition
            ? new ViewEdit(definition, dialog.NamePlan.Change)
            : null;
    }

    /// <summary>Save waits for both; the name's problem, or a warning, shows under it.</summary>
    private void ShowSave()
    {
        var plan = NamePlan;
        var canName = plan.Problem is RenameProblem.None or RenameProblem.Unchanged;
        SaveButton.IsEnabled = canName && Editor.Draft.Definition is not null;
        NameStatus.Text = plan switch
        {
            { Problem: RenameProblem.Empty or RenameProblem.LinkPhrase } => plan.Status,
            { Problem: RenameProblem.None, Homonyms.Count: > 0 } => $"“{plan.Name}” exists already: saving keeps both.",
            _ => string.Empty,
        };
        NameStatus.Visibility = NameStatus.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (SaveButton.IsEnabled)
        {
            DialogResult = true;
        }
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindows.RemoveMinimizeMaximizeButtons(new WindowInteropHelper(this).Handle);
}
