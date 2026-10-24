using System.Windows;
using System.Windows.Shapes;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Server;
using Kofrenot.Core.Settings;

namespace Kofrenot.UI.Views;

/// <summary>The sync of the open server vault, in the header: how it stands, at a glance.</summary>
public partial class MainWindow
{
    private SyncStatus? Sync => _session.Current is not null && _session.Selected?.Location.Store is { } store ? _session.Sync.StatusOf(store) : null;

    private void ShowSync()
    {
        SyncButton.Visibility = Sync is null ? Visibility.Collapsed : Visibility.Visible;
        if (Sync is not { } status)
        {
            return;
        }

        SyncText.Text = status.Summary;
        SyncButton.ToolTip = status.Details + (status.Failure == ServerFailure.SignInNeeded ? "\n\nClick: Settings > Server." : "\n\nClick: sync now.");
        SyncDot.SetResourceReference(Shape.FillProperty, status.Level switch
        {
            SyncLevel.Synced => "SystemFillColorSuccessBrush",
            SyncLevel.Waiting => "SystemFillColorNeutralBrush",
            SyncLevel.Offline => "SystemFillColorCautionBrush",
            _ => "SystemFillColorCriticalBrush",
        });
    }

    private void SyncButton_Click(object sender, RoutedEventArgs e)
    {
        if (Sync?.Failure == ServerFailure.SignInNeeded)
        {
            ShowSettings(SettingsCatalog.ServerTab);
            return;
        }

        _log.Info("Action", "Sync now");
        _ = _session.Sync.SyncNowAsync();
    }
}
