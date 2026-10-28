using System.Windows;
using Kofrenot.Core.Logging;

namespace Kofrenot.UI.Views;

/// <summary>Settings > Logging: this computer's exchanges with the server, per day then by kind, to copy as text.</summary>
public partial class SettingsWindow
{
    private void ShowExchanges()
    {
        var exchanges = _session.Server.Exchanges;
        ExchangeDays.ItemsSource = exchanges.DayRows();
        ExchangeKinds.ItemsSource = exchanges.KindRows();
    }

    private void CopyExchanges_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(_session.Server.Exchanges.Text());
        _log.Info("Action", "Exchange statistics copied");
    }
}
