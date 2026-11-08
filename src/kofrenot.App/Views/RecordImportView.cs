using System.ComponentModel;
using Kofrenot.Core.Imports;

namespace Kofrenot.UI.Views;

/// <summary>
/// A record of the preview: its name and what it does (<see cref="RecordItem.Details"/>), ticked when
/// added or updated, or one of three choices when to check. <see cref="Changed"/> tells the dialog the
/// count to write changed.
/// </summary>
public sealed class RecordImportView(RecordItem item) : INotifyPropertyChanged
{
    private RecordChoice _choice = item.IsChosen ? RecordChoice.Import : RecordChoice.Skip;

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action? Changed;

    public RecordItem Item { get; } = item;

    public string Name => Item.Name ?? $"Record {Item.Record.Number}";

    public string Details => Item.Reason ?? string.Join("\n", Item.Details);

    public bool HasCheck => Item.Outcome is RecordOutcome.New or RecordOutcome.Updated;

    public bool HasChoice => Item.Outcome == RecordOutcome.ToCheck;

    /// <summary>The group of its choices: one per record.</summary>
    public string ChoiceGroup => $"record-{Item.Record.Number}";

    public RecordChoice Choice => _choice;

    public bool IsChecked
    {
        get => _choice != RecordChoice.Skip;
        set => Choose(value ? RecordChoice.Import : RecordChoice.Skip);
    }

    public bool IsUpdate
    {
        get => _choice == RecordChoice.Import;
        set => Choose(RecordChoice.Import, value);
    }

    public bool IsAddAsNew
    {
        get => _choice == RecordChoice.AddAsNew;
        set => Choose(RecordChoice.AddAsNew, value);
    }

    public bool IsSkip
    {
        get => _choice == RecordChoice.Skip;
        set => Choose(RecordChoice.Skip, value);
    }

    // A radio button unchecked says nothing: the one checked says the choice.
    private void Choose(RecordChoice choice, bool isChecked = true)
    {
        if (!isChecked || choice == _choice)
        {
            return;
        }

        _choice = choice;
        foreach (var name in new[] { nameof(IsChecked), nameof(IsUpdate), nameof(IsAddAsNew), nameof(IsSkip) })
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        Changed?.Invoke();
    }
}

/// <summary>A row of a mapping's sample table: a record, what importing it would do, and its details, a line each.</summary>
public sealed record SampleRow(int Number, string Name, string Result, string Details)
{
    public static SampleRow Of(RecordItem item) => new(
        item.Record.Number,
        item.Name ?? string.Empty,
        item.Outcome switch
        {
            RecordOutcome.ToCheck => "To check",
            RecordOutcome.New => "New",
            RecordOutcome.Updated => "Updated",
            RecordOutcome.Unchanged => "Unchanged",
            _ => "Not imported",
        },
        string.Join("\n", (item.Reason is { } reason ? [reason] : item.Details).Concat(item.ValueProblems.Select(problem => $"not imported: {problem}"))));
}

/// <summary>A group of the preview (to check, new, updated, not imported, unchanged), open but for the unchanged.</summary>
public sealed record RecordGroupView(string Title, IReadOnlyList<RecordImportView> Rows, bool IsExpanded)
{
    public string Header => $"{Title} ({Rows.Count})";
}
