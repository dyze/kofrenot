using System.Windows;
using System.Windows.Interop;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Views;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>Edits what a view shows: its definition, saved by the caller. Save waits for a complete definition (a tree's root).</summary>
public partial class ViewDialog : Window
{
    private ViewDialog(Window owner, KnowledgeGraph graph, Node view, ViewState loaded)
    {
        InitializeComponent();
        Owner = owner;
        TitleText.Text = $"What “{view.Name}” shows";
        ConflictText.Visibility = loaded.IsConflict ? Visibility.Visible : Visibility.Collapsed;
        Editor.Setup(graph.Types, text => ViewDraft.FindRoots(graph, text));
        Editor.Load(loaded.Definition is { } definition ? ViewDraft.Of(definition, graph) : ViewDraft.Default);
        Editor.Changed += ShowSave;
        ShowSave();
        Loaded += (_, _) => Editor.FocusFirst();
    }

    /// <summary>Shows the dialog: the definition to save, or null when cancelled.</summary>
    public static ViewDefinition? Ask(Window owner, KnowledgeGraph graph, Node view, ViewState loaded)
    {
        var dialog = new ViewDialog(owner, graph, view, loaded);
        return dialog.ShowDialog() == true ? dialog.Editor.Draft.Definition : null;
    }

    private void ShowSave() => SaveButton.IsEnabled = Editor.Draft.Definition is not null;

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (Editor.Draft.Definition is not null)
        {
            DialogResult = true;
        }
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindows.RemoveMinimizeMaximizeButtons(new WindowInteropHelper(this).Handle);
}
