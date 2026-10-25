using Avalonia.Controls;
using Avalonia.Interactivity;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Server;

namespace Kofrenot.Phone.Views;

/// <summary>The server account: its server, email and password. Only the session is kept, never the password.</summary>
public sealed partial class SignInPage : UserControl, IPage
{
    private readonly Shell _shell;

    public SignInPage(Shell shell)
    {
        _shell = shell;
        InitializeComponent();
        var account = shell.Services.Server.Account;
        ServerBox.Text = account?.Server.ToString() ?? "https://";
        EmailBox.Text = account?.Email;
    }

    public bool GoBack() => false;

    public void Refresh()
    {
    }

    private async void SignIn_Click(object? sender, RoutedEventArgs e)
    {
        // HTTPS, but to the phone itself (a server reached through "adb reverse" while developing).
        if (!Uri.TryCreate(ServerBox.Text?.Trim(), UriKind.Absolute, out var server) || (server.Scheme != Uri.UriSchemeHttps && !(server.Scheme == Uri.UriSchemeHttp && server.IsLoopback)))
        {
            ShowError("Type the server's address, starting with https://.");
            return;
        }

        _shell.Services.Log.Info("Action", "Sign in");
        SetBusy(true);
        try
        {
            await _shell.Services.Server.SignInAsync(server, EmailBox.Text?.Trim() ?? string.Empty, PasswordBox.Text ?? string.Empty);
            PasswordBox.Text = string.Empty;
            _shell.Show(new VaultsPage(_shell));
        }
        catch (ServerException ex)
        {
            ShowError(ex.Failure switch
            {
                ServerFailure.Offline => "The server cannot be reached. Check the address and the connection.",
                ServerFailure.Refused => "Wrong email or password.",
                ServerFailure.TooManyRequests => "Too many attempts: try again in a minute.",
                _ => $"The server failed ({ex.Message}).",
            });
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ShowError(string message)
    {
        Error.Text = message;
        Error.IsVisible = true;
    }

    private void SetBusy(bool busy)
    {
        Busy.IsVisible = busy;
        SignInButton.IsEnabled = !busy;
        Error.IsVisible = Error.IsVisible && !busy;
    }
}
