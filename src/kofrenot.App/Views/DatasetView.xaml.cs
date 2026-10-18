using System.IO;
using System.Windows;
using System.Windows.Controls;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;

namespace Kofrenot.UI.Views;

/// <summary>
/// Shows a dataset version's text in its details (<see cref="DatasetType"/>), read off the UI
/// thread, in memory only: nothing decrypted is written to disk. An image opens in its own window
/// (<see cref="ImageWindow"/>) instead; another type says to export the file.
/// </summary>
public partial class DatasetView : UserControl
{
    // Shared by every text shown, while kofrenot runs.
    private static bool _wrapsLines;

    private Guid? _shown;
    private int _loading;

    public DatasetView()
    {
        InitializeComponent();
        ShowWrap();
    }

    /// <summary>Raised when the view starts or stops showing a text, so the details give it room.</summary>
    public event Action? ContentChanged;

    public ILog Log { get; set; } = NullLog.Instance;

    /// <summary>A text is shown.</summary>
    public bool IsShowing => TextView.Visibility == Visibility.Visible;

    /// <summary>The text shown has the keyboard: the details leave its keys alone.</summary>
    public bool IsReading => TextView.IsKeyboardFocusWithin;

    /// <summary>Shows a version's file, unless it is shown already; null, or a file not synced yet, shows nothing.</summary>
    public void Show(DatasetFiles files, DatasetEdited? version, bool isAvailable)
    {
        if (version is not null && isAvailable && version.Id == _shown)
        {
            return;
        }

        Clear();
        if (version is null || !isAvailable)
        {
            return;
        }

        _shown = version.Id;
        if (DatasetType.WhyNotShown(version.File) is { } why)
        {
            Visibility = Visibility.Visible;
            MessageText.Text = why;
        }
        else if (DatasetType.Of(version.File).Viewer == DatasetViewer.Text)
        {
            Visibility = Visibility.Visible;
            _ = Load(files, version);
        }
    }

    /// <summary>Shows nothing, and forgets a file being read.</summary>
    public void Clear()
    {
        _loading++;
        _shown = null;
        var hadContent = IsShowing;
        TextView.Text = string.Empty;
        TextView.Visibility = Visibility.Collapsed;
        WrapBox.Visibility = Visibility.Collapsed;
        MessageText.Text = string.Empty;
        Visibility = Visibility.Collapsed;
        if (hadContent)
        {
            ContentChanged?.Invoke();
        }
    }

    private async Task Load(DatasetFiles files, DatasetEdited version)
    {
        var loading = ++_loading;
        MessageText.Text = "Loading…";
        try
        {
            var text = await Task.Run(() =>
            {
                using var content = files.OpenRead(version.Id);
                return TextPreview.Read(content);
            });
            if (loading != _loading)
            {
                return;
            }

            TextView.Text = text.Text;
            TextView.Visibility = Visibility.Visible;
            WrapBox.Visibility = Visibility.Visible;
            MessageText.Text = text.IsCut ? "Only the beginning is shown: export the file to see all of it." : string.Empty;
            ContentChanged?.Invoke();
            Log.Info("Action", "Dataset viewed (text)");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            if (loading == _loading)
            {
                Log.Warn("Action", $"Dataset cannot be shown ({ex.GetType().Name})");
                MessageText.Text = "This file cannot be shown here: export it to open it.";
            }
        }
    }

    private void WrapBox_Click(object sender, RoutedEventArgs e)
    {
        _wrapsLines = WrapBox.IsChecked == true;
        ShowWrap();
    }

    private void ShowWrap()
    {
        WrapBox.IsChecked = _wrapsLines;
        TextView.TextWrapping = _wrapsLines ? TextWrapping.Wrap : TextWrapping.NoWrap;
        TextView.HorizontalScrollBarVisibility = _wrapsLines ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
    }
}
