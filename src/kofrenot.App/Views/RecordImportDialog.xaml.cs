using System.Windows;
using System.Windows.Interop;
using Kofrenot.Core.Imports;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>
/// The preview of an import (<see cref="RecordPlan"/>): its records by group, each to choose, the
/// values not imported and the rules left out. Nothing is written here: the choices are returned.
/// </summary>
public partial class RecordImportDialog : Window
{
    private static readonly (RecordOutcome Outcome, string Title)[] Groups =
    [
        (RecordOutcome.ToCheck, "To check"),
        (RecordOutcome.New, "New"),
        (RecordOutcome.Updated, "Updated"),
        (RecordOutcome.NotImported, "Not imported"),
        (RecordOutcome.Unchanged, "Unchanged"),
    ];

    private readonly RecordPlan _plan;
    private readonly Dictionary<RecordItem, RecordImportView> _rows;

    private RecordImportDialog(Window owner, string mapping, string file, RecordPlan plan)
    {
        InitializeComponent();
        Owner = owner;
        _plan = plan;
        _rows = plan.Items.ToDictionary(item => item, item => new RecordImportView(item));
        foreach (var row in _rows.Values)
        {
            row.Changed += ShowCount;
        }

        HeadingText.Text = $"Import “{mapping}” · {file}";
        var groups = Groups
            .Select(group => new RecordGroupView(group.Title, plan.Of(group.Outcome).Select(item => _rows[item]).ToList(), group.Outcome != RecordOutcome.Unchanged))
            .Where(group => group.Rows.Count > 0)
            .ToList();
        GroupList.ItemsSource = groups;
        CountList.ItemsSource = groups;
        var problems = plan.Warnings.Concat(plan.Items.SelectMany(item => item.ValueProblems.Select(problem => $"{_rows[item].Name} · {problem}"))).ToList();
        ProblemsText.Text = problems.Count == 0 ? string.Empty : $"Not imported: {string.Join("\n", problems)}";
        ProblemsText.Visibility = problems.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        ShowCount();
    }

    /// <summary>The choices made, by record; null when cancelled.</summary>
    public static Func<RecordItem, RecordChoice>? Ask(Window owner, string mapping, string file, RecordPlan plan)
    {
        var dialog = new RecordImportDialog(owner, mapping, file, plan);
        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        var choices = dialog._rows.ToDictionary(pair => pair.Key, pair => pair.Value.Choice);
        return item => choices[item];
    }

    private Func<RecordItem, RecordChoice> Choose => item => _rows[item].Choice;

    private void ShowCount()
    {
        var count = _plan.CountFor(Choose);
        ImportButton.Content = $"Import {count}";
        ImportButton.IsEnabled = count > 0;
    }

    // A count scrolls to its group.
    private void Count_Click(object sender, RoutedEventArgs e)
    {
        var group = ((FrameworkElement)sender).Tag;
        if (GroupList.ItemContainerGenerator.ContainerFromItem(group) is FrameworkElement container)
        {
            container.BringIntoView(new Rect(0, 0, container.ActualWidth, container.ActualHeight));
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindows.RemoveMinimizeMaximizeButtons(new WindowInteropHelper(this).Handle);
}
