using System.Security;
using System.Windows;
using Kofrenot.Core;
using Kofrenot.Core.Logging;

namespace Kofrenot.UI.Views;

/// <summary>The General tab's "Start kofrenot with Windows" box.</summary>
public partial class SettingsWindow
{
    /// <summary>
    /// Ticked when Windows starts this copy. When it starts another one, says which: ticking the
    /// box then makes Windows start this copy instead.
    /// </summary>
    private void ShowAutoStart()
    {
        AutoStartStatus status;
        string? program;
        try
        {
            status = _autoStart.Status;
            program = _autoStart.RegisteredProgram;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
        {
            _log.Warn("Settings", $"Cannot read start with Windows: {ex.GetType().Name}: {ex.Message}");
            status = AutoStartStatus.Off;
            program = null;
        }

        AutoStartCheckBox.IsChecked = status == AutoStartStatus.ThisCopy;
        AutoStartNote.Text = $"Windows starts another copy: {program}. Tick the box to start this one instead.";
        AutoStartNote.Visibility = status == AutoStartStatus.OtherCopy ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AutoStartCheckBox_Click(object sender, RoutedEventArgs e)
    {
        var wanted = AutoStartCheckBox.IsChecked == true;

        try
        {
            _autoStart.SetEnabled(wanted);
            _log.Info("Settings", $"Start with Windows {(wanted ? "on" : "off")}");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
        {
            _log.Error("Settings", "Cannot change start with Windows", ex);
            ShowMessage(ex.Message);
        }

        ShowAutoStart();
    }
}
