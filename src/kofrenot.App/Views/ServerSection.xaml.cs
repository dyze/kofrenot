using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Server;
using Kofrenot.Core.Session;
using Kofrenot.Core.Storage;

namespace Kofrenot.UI.Views;

/// <summary>Settings > Server: signs up, in and out of the account that keeps the server vaults.</summary>
public partial class ServerSection : UserControl
{
    private ServerConnection? _connection;

    public ServerSection()
    {
        InitializeComponent();
        Unloaded += (_, _) =>
        {
            if (_connection is not null)
            {
                _connection.Changed -= Show;
            }
        };
    }

    public ILog Log { private get; set; } = NullLog.Instance;

    /// <summary>Chooses or changes the account's pass phrase (<see cref="VaultSession.SetAccountPassPhrase"/>).</summary>
    public Action<Window>? SetPassPhrase { private get; set; }

    // Signed in to an account without a pass phrase: others cannot share with it yet.
    private bool NeedsPassPhrase => _connection is { IsSignedIn: true, TooOld: null, Identity: null };

    /// <summary>The account shown; set once by the Settings window.</summary>
    public ServerConnection Connection
    {
        set
        {
            _connection = value;
            value.Changed += Show;
            ServerBox.Text = value.Account?.Server.ToString() ?? string.Empty;
            EmailBox.Text = value.Account?.Email ?? string.Empty;
            Show();
        }
    }

    /// <summary>
    /// The address typed, when it is one: https, or http on this computer only (a server being
    /// tried), so a password never travels in clear.
    /// </summary>
    public static Uri? AddressOf(string text) =>
        Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
            ? uri
            : null;

    private void Show()
    {
        if (_connection is not { } connection)
        {
            return;
        }

        var signedIn = connection.IsSignedIn;
        SignOutButton.IsEnabled = signedIn;
        StatusText.Text = (connection.Account, signedIn) switch
        {
            ({ } account, true) => $"Signed in as {account.Email} on {account.Server.Host}.",
            ({ } account, false) => $"Signed out of {account.Server.Host}: sign in to sync the server vaults.",
            _ => "No server yet.",
        };
        TooOldText.Text = connection.TooOld ?? string.Empty;
        TooOldText.Visibility = connection.TooOld is null ? Visibility.Collapsed : Visibility.Visible;
        var hasPassPhrase = connection.Identity is not null;
        PassPhraseButton.IsEnabled = signedIn && connection.TooOld is null;
        PassPhraseButton.Content = hasPassPhrase ? "Change..." : "Choose...";
        PassPhraseButton.ToolTip = hasPassPhrase
            ? "The account's pass phrase opens all your server vaults: it is sealed again, the recovery key stays the same"
            : "The account's pass phrase opens all your server vaults, and lets others share theirs with you";
        ChangePasswordButton.IsEnabled = signedIn;
        KeyText.Text = connection.Identity is { } identity ? $"Your key: {Fingerprint.Of(identity.Identity.PublicKey, identity.Identity.SigningKey)}" : string.Empty;
        KeyText.Visibility = connection.Identity is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
    {
        if (_connection is not { Client: { } client, Account: { } account })
        {
            return;
        }

        Log.Info("Action", "Change the server password");
        if (PasswordDialog.Ask(Window.GetWindow(this), $"{account.Email} on {account.Server.Host}", async (current, password) =>
        {
            try
            {
                await client.ChangePasswordAsync(current, password);
                return null;
            }
            catch (ServerException ex)
            {
                Log.Warn("Action", $"Password change refused: {ex.Message}");
                return Explain(ex);
            }
        }))
        {
            StatusText.Text = "Password changed: sign in with it on your other devices.";
        }
        else
        {
            Log.Info("Action", "Password change cancelled");
        }
    }

    private void PassPhraseButton_Click(object sender, RoutedEventArgs e) => SetAccountPassPhrase();

    private void SetAccountPassPhrase()
    {
        SetPassPhrase?.Invoke(Window.GetWindow(this));
        Show();
    }

    // Right after signing in: the pass phrase lets others share with this account, so it is offered then.
    private void OfferPassPhrase()
    {
        if (!NeedsPassPhrase)
        {
            return;
        }

        if (MessageDialog.Confirm(
            Window.GetWindow(this),
            "Choose your account's pass phrase now?\n\nIt opens all your server vaults, and lets others share theirs with you.",
            "Choose...",
            "Later"))
        {
            SetAccountPassPhrase();
        }
        else
        {
            Log.Info("Action", "Server account pass phrase offered after sign in: later");
        }
    }

    private async void SignInButton_Click(object sender, RoutedEventArgs e)
    {
        if (await Call("Sign in", (connection, server, email, password) => connection.SignInAsync(server, email, password), signedUp: false))
        {
            OfferPassPhrase();
        }
    }

    private async void SignUpButton_Click(object sender, RoutedEventArgs e) =>
        await Call("Sign up", (connection, server, email, password) => connection.SignUpAsync(server, email, password), signedUp: true);

    private void SignOutButton_Click(object sender, RoutedEventArgs e) => _connection?.SignOut();

    /// <summary>True when the server took it.</summary>
    private async Task<bool> Call(string action, Func<ServerConnection, Uri, string, string, Task> call, bool signedUp)
    {
        if (_connection is not { } connection)
        {
            return false;
        }

        if (AddressOf(ServerBox.Text) is not { } server)
        {
            StatusText.Text = "Type the server's address, as https://kofre.example.org.";
            return false;
        }

        var email = EmailBox.Text.Trim();
        if (email.Length == 0 || PasswordBox.Password.Length == 0)
        {
            StatusText.Text = "Type your email and password.";
            return false;
        }

        Log.Info("Action", action);
        IsEnabled = false;
        Cursor = Cursors.Wait;
        try
        {
            await call(connection, server, email, PasswordBox.Password);
            PasswordBox.Clear();
            Show();
            if (signedUp)
            {
                StatusText.Text = "Account created: you can sign in once the server's admin approves it.";
            }

            return true;
        }
        catch (ServerException ex)
        {
            Log.Warn("Action", $"{action} refused: {ex.Message}");
            StatusText.Text = Explain(ex);
            return false;
        }
        finally
        {
            IsEnabled = true;
            Cursor = null;
        }
    }

    private static string Explain(ServerException ex) => (ex.Failure, ex.Code) switch
    {
        (ServerFailure.Offline, _) => "The server cannot be reached. Check its address and the connection.",
        (_, "wrong-credentials") => "Wrong email or password.",
        (_, "account-not-approved") => "Your account waits for the server's admin to approve it.",
        (_, "account-disabled") => "Your account is disabled: ask the server's admin.",
        (_, "email-taken") => "An account with this email exists already: sign in.",
        (_, "sign-up-closed") => "The server takes no new account for now: ask its admin.",
        (_, "invalid-email") => "This email is not valid.",
        (_, "weak-password") => "The password needs at least 10 characters.",
        (_, "wrong-password") => "The current password is not this one.",
        (ServerFailure.TooManyRequests, _) => "Too many attempts: try again in a minute.",
        _ => $"The server failed ({ex.Message}).",
    };
}
