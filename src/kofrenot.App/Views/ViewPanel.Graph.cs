using System.Windows;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Logging;

namespace Kofrenot.UI.Views;

/// <summary>
/// A graph's buttons on the bar: Fit, Reset layout, and Link names (a toggle kept per view in
/// <see cref="ViewStates"/>), which writes the name of each link on its line.
/// </summary>
public partial class ViewPanel
{
    private bool ShowsLinkNames => _view is not null && States?.ShowsLinkNames(_vault, _view.Id) == true;

    private void Fit_Click(object sender, RoutedEventArgs e)
    {
        Log.Info("Action", "Graph view fitted");
        GraphBoard.Fit();
        GraphBoard.FocusContent();
    }

    private void ResetLayout_Click(object sender, RoutedEventArgs e) => GraphBoard.ResetLayout();

    private void LinkNames_Click(object sender, RoutedEventArgs e)
    {
        if (_view is null || States is null)
        {
            return;
        }

        var showsLinkNames = LinkNamesButton.IsChecked == true;
        States.SetShowsLinkNames(_vault, _view.Id, showsLinkNames);
        Log.Info("Action", showsLinkNames ? "Graph link names shown" : "Graph link names hidden");
        Refresh();
        GraphBoard.FocusContent();
    }
}
