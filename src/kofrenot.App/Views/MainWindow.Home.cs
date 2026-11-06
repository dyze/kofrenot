using System.Windows;
using System.Windows.Threading;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>
/// The home (the empty input box's rows) fits the window: as many recent nodes as its height holds,
/// in columns when it is wide (<see cref="HomeRoom"/>). Measured from the rows shown, once laid out.
/// </summary>
public partial class MainWindow
{
    // Until a row is laid out; then the tallest seen, so that the fit settles.
    private double _rowHeight = 28;
    private double _headerHeight = 22;

    private void ResultsArea_SizeChanged(object sender, SizeChangedEventArgs e) => FitHome();

    /// <summary>Once the rows shown are laid out: measures them, and fits the home again if they differ.</summary>
    private void FitHomeLater() => Dispatcher.BeginInvoke(FitHome, DispatcherPriority.Loaded);

    private void FitHome()
    {
        if (ResultsArea.ActualWidth <= 0 || ResultsArea.ActualHeight <= 0)
        {
            return;
        }

        MeasureRows();
        var room = new HomeRoom(ResultsArea.ActualWidth, ResultsArea.ActualHeight, _rowHeight, _headerHeight);
        if (room == _input.Room)
        {
            return;
        }

        _input.Room = room;
        if (_session.Current is not null && string.IsNullOrWhiteSpace(InputBox.Input.Text))
        {
            ShowInput();
        }
    }

    private void MeasureRows()
    {
        for (var index = 0; index < ResultsList.Items.Count; index++)
        {
            if (ResultsList.Items[index] is ResultRow { IsLink: false } row
                && ResultsList.ItemContainerGenerator.ContainerFromIndex(index) is FrameworkElement { ActualHeight: > 0 } shown)
            {
                if (row.IsHeader)
                {
                    _headerHeight = Math.Max(_headerHeight, shown.ActualHeight);
                }
                else
                {
                    _rowHeight = Math.Max(_rowHeight, shown.ActualHeight);
                }
            }
        }
    }
}
