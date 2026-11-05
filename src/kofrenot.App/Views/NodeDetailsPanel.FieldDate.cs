using System.Globalization;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;

namespace Kofrenot.UI.Views;

/// <summary>
/// A date field's calendar, beside its box: the date chosen is written as typed one is
/// (2026-10-27), at once; typing in the box still works. Esc or a click elsewhere closes it.
/// </summary>
public partial class NodeDetailsPanel
{
    private void ShowCalendar(NodeField field, FieldHolder holder)
    {
        if (!_boxes.TryGetValue((holder.Id, field.Owner, field.Field), out var box))
        {
            return;
        }

        var shown = FieldValue.FirstDayOf(field.Value)?.ToDateTime(TimeOnly.MinValue);
        var calendar = new System.Windows.Controls.Calendar { SelectedDate = shown, DisplayDate = shown ?? DateTime.Today };
        var popup = new Popup { PlacementTarget = box, Placement = PlacementMode.Bottom, StaysOpen = false, Child = calendar };
        calendar.SelectedDatesChanged += (_, _) =>
        {
            popup.IsOpen = false;
            if (calendar.SelectedDate is { } chosen)
            {
                WriteFields(planner => planner.Set(holder, field, chosen.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), "Field set", field, holder);
            }
        };
        popup.IsOpen = true;
        calendar.Focus();
    }
}
