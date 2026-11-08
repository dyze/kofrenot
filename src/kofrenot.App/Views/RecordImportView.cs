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

    /// <summary>A record in one line, for a sample: <i>Dune: new</i>, <i>Record 3: not imported, no “title”</i>.</summary>
    public static string Summary(RecordItem item)
    {
        var name = item.Name ?? $"Record {item.Record.Number}";
        var outcome = item.Outcome switch
        {
            RecordOutcome.ToCheck => "to check",
            RecordOutcome.New => "new",
            RecordOutcome.Updated => "updated",
            RecordOutcome.Unchanged => "unchanged",
            _ => "not imported",
        };
        var more = item.Reason ?? (item.Details.Count > 0 ? string.Join(" · ", item.Details) : null);
        return more is null ? $"{name}: {outcome}" : $"{name}: {outcome}, {more}";
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

/// <summary>A group of the preview (to check, new, updated, not imported, unchanged), open but for the unchanged.</summary>
public sealed record RecordGroupView(string Title, IReadOnlyList<RecordImportView> Rows, bool IsExpanded)
{
    public string Header => $"{Title} ({Rows.Count})";
}
