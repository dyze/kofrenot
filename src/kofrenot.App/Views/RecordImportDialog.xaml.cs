using System.Windows;
using System.Windows.Interop;
using Kofrenot.Core.Imports;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>
/// The preview of an import: of a file's records (<see cref="RecordPlan"/>) or of another vault's nodes
/// (<see cref="VaultImportPlan"/>). Its rows by group, each to choose, then what is not imported. Nothing
/// is written here: the choices are returned.
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

    private readonly Func<int> _count;

    private RecordImportDialog(Window owner, string heading, IReadOnlyList<RecordImportView> rows, Func<int> count, IReadOnlyList<string> notes)
    {
        InitializeComponent();
        Owner = owner;
        _count = count;
        foreach (var row in rows)
        {
            row.Changed += ShowCount;
        }

        HeadingText.Text = heading;
        var groups = Groups
            .Select(group => new RecordGroupView(group.Title, rows.Where(row => row.Outcome == group.Outcome).ToList(), group.Outcome != RecordOutcome.Unchanged))
            .Where(group => group.Rows.Count > 0)
            .ToList();
        GroupList.ItemsSource = groups;
        CountList.ItemsSource = groups;
        ProblemsText.Text = string.Join("\n", notes);
        ProblemsText.Visibility = notes.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        ShowCount();
    }

    /// <summary>The choices made for a file's records; null when cancelled.</summary>
    public static Func<RecordItem, RecordChoice>? Ask(Window owner, string mapping, string file, RecordPlan plan)
    {
        var problems = plan.Warnings.Concat(plan.Items.SelectMany(item => item.ValueProblems.Select(problem => $"{RecordImportView.Of(item).Name} · {problem}"))).ToList();
        return Ask(owner, $"Import “{mapping}” · {file}", plan.Items, RecordImportView.Of, plan.CountFor, problems.Count == 0 ? [] : [$"Not imported: {string.Join("\n", problems)}"]);
    }

    /// <summary>The choices made for another vault's nodes; null when cancelled.</summary>
    public static Func<VaultImportItem, RecordChoice>? Ask(Window owner, string vault, string query, VaultImportPlan plan) =>
        Ask(owner, $"Import from “{vault}” · {query}", plan.Items, RecordImportView.Of, plan.CountFor, plan.Summary);

    private static Func<T, RecordChoice>? Ask<T>(Window owner, string heading, IReadOnlyList<T> items, Func<T, RecordImportView> viewOf, Func<Func<T, RecordChoice>, int> count, IReadOnlyList<string> notes)
        where T : notnull
    {
        var rows = items.ToDictionary(item => item, viewOf);
        RecordChoice Choose(T item) => rows[item].Choice;
        var dialog = new RecordImportDialog(owner, heading, [.. rows.Values], () => count(Choose), notes);
        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        var choices = rows.ToDictionary(pair => pair.Key, pair => pair.Value.Choice);
        return item => choices[item];
    }

    private void ShowCount()
    {
        var count = _count();
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
