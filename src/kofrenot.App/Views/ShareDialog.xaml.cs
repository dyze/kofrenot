using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Server;
using Kofrenot.Core.Session;
using Kofrenot.Core.Sharing;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>
/// Shares a server vault: finds someone by email (<c>find</c>), shows their key's fingerprint
/// to compare with them out of band, then asks their role. Share is enabled once it matches.
/// </summary>
public partial class ShareDialog : Window
{
    private readonly Func<string, Task<(Lookup Result, ShareCandidate? Candidate)>> _find;
    private ShareCandidate? _found;

    private ShareDialog(Window owner, string place, Func<string, Task<(Lookup, ShareCandidate?)>> find)
    {
        InitializeComponent();
        Owner = owner;
        _find = find;
        PlaceText.Text = place;
        Loaded += (_, _) => EmailBox.Focus();
    }

    /// <summary>Who to share with, and as what; null when cancelled.</summary>
    public static ShareChoice? Ask(Window owner, string place, Func<string, Task<(Lookup, ShareCandidate?)>> find)
    {
        var dialog = new ShareDialog(owner, place, find);
        return dialog.ShowDialog() == true && dialog._found is { } found
            ? new ShareChoice(found, dialog.RoleBox.SelectedIndex == 1 ? MemberRole.Writer : MemberRole.Reader)
            : null;
    }

    private async void FindButton_Click(object sender, RoutedEventArgs e)
    {
        if (EmailBox.Text.Trim().Length == 0)
        {
            ShowError("Type their email.");
            return;
        }

        IsEnabled = false;
        Cursor = Cursors.Wait;
        try
        {
            var (result, candidate) = await _find(EmailBox.Text);
            switch (result)
            {
                case Lookup.Found:
                    _found = candidate;
                    FingerprintBox.Text = candidate!.Fingerprint;
                    FoundPanel.Visibility = Visibility.Visible;
                    ErrorText.Visibility = Visibility.Collapsed;
                    MakeDefault(ShareButton);
                    break;
                case Lookup.NoKeyYet:
                    ShowError("This account has no pass phrase yet: ask them to sign in with kofrenot 0.7 or later, and choose it (Settings > Server > Pass phrase > Choose...).");
                    break;
                default:
                    ShowError("No account on this server has this email.");
                    break;
            }
        }
        catch (ServerException ex)
        {
            ShowError(ex.Failure switch
            {
                ServerFailure.Offline => "The server cannot be reached.",
                ServerFailure.TooManyRequests => "Too many lookups: try again in a minute.",
                _ => $"The server failed ({ex.Message}).",
            });
        }
        finally
        {
            IsEnabled = true;
            Cursor = null;
        }
    }

    // Another email: the key found before is not theirs.
    private void EmailBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _found = null;
        FoundPanel.Visibility = Visibility.Collapsed;
        MatchesCheckBox.IsChecked = false;
        MakeDefault(FindButton);
    }

    // Enter presses Find until someone is found, then Share; the accent shows which.
    private void MakeDefault(Button button)
    {
        var other = button == FindButton ? ShareButton : FindButton;
        other.IsDefault = false;
        other.ClearValue(StyleProperty);
        button.IsDefault = true;
        button.SetResourceReference(StyleProperty, "AccentButtonStyle");
    }

    private void MatchesCheckBox_Changed(object sender, RoutedEventArgs e) => ShareButton.IsEnabled = MatchesCheckBox.IsChecked == true && _found is not null;

    private void ShareButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindows.RemoveMinimizeMaximizeButtons(new WindowInteropHelper(this).Handle);
}
