using System.Windows;
using Forms = System.Windows.Forms;

namespace Kofrenot.UI.Tray;

/// <summary>
/// The icon in the Windows notification area. A click shows the main window; the menu offers
/// Show, Settings, Log window (ticked while it is open: a click shows or hides it) and Quit.
/// Uses WinForms' NotifyIcon, since WPF has none.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _logItem;

    /// <param name="version">Shown in the tooltip.</param>
    /// <param name="isLogOpen">Whether the log window is open, read each time the menu opens.</param>
    /// <param name="toggleLog">Shows the log window, or closes it when it is open.</param>
    public TrayIcon(
        string version,
        Action show,
        Action showSettings,
        Func<bool> isLogOpen,
        Action toggleLog,
        Action quit)
    {
        var showItem = new Forms.ToolStripMenuItem("Show kofrenot", null, (_, _) => show());
        showItem.Font = new System.Drawing.Font(showItem.Font, System.Drawing.FontStyle.Bold);

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(showItem);
        menu.Items.Add(new Forms.ToolStripMenuItem("Settings", null, (_, _) => showSettings()));
        _logItem = new Forms.ToolStripMenuItem("Log window", null, (_, _) => toggleLog());
        menu.Items.Add(_logItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("Quit", null, (_, _) => quit()));
        menu.Opening += (_, _) => _logItem.Checked = isLogOpen();

        _icon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = $"kofrenot {version}",
            ContextMenuStrip = menu,
            Visible = true,
        };

        // A left click shows the main window; the right click opens the menu by itself.
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                show();
            }
        };
    }

    public void Dispose()
    {
        // Hide first, or the icon lingers in the notification area until the mouse passes over it.
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Icon?.Dispose();
        _icon.Dispose();
    }

    /// <summary>The application icon embedded in the assembly (see kofrenot.App.csproj), at the tray size.</summary>
    private static System.Drawing.Icon LoadIcon()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/kofrenot.ico"))
            ?? throw new InvalidOperationException("kofrenot.ico is missing from the assembly resources.");
        using var stream = resource.Stream;
        return new System.Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
    }
}
