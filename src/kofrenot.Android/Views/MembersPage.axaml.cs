using Avalonia.Controls;
using Avalonia.Interactivity;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Session;
using Kofrenot.Core.Sharing;
using Kofrenot.Phone.Platform;

namespace Kofrenot.Phone.Views;

/// <summary>
/// The open vault's members on the phone: its owner shares it with an account (found by its email,
/// its fingerprint shown to compare) and makes a member a reader or a writer; removing one is done on
/// Windows, as it rotates the vault's key. A member of a vault shared with them leaves it.
/// </summary>
public sealed partial class MembersPage : UserControl, IPage
{
    private readonly Shell _shell;

    public MembersPage(Shell shell)
    {
        _shell = shell;
        InitializeComponent();
        Refresh();
    }

    public bool GoBack() => false;

    public void SyncChanged()
    {
    }

    public void Refresh()
    {
        var session = _shell.Services.Session;
        MembersList.ItemsSource = session.Members;
        SharePanel.IsVisible = session.CanShare;
        LeaveButton.IsVisible = session.CanLeave;
        Intro.Text = session.CanShare
            ? "Share this vault with another account of your server. A tap on a member makes them a reader or a writer; removing one is done in kofrenot on Windows, as it changes the vault's key."
            : session.CanLeave
                ? $"This vault is shared with you as a {session.MyRole?.ToString().ToLowerInvariant()}."
                : "This vault is not shared. Its owner shares it, from a server vault opened with their account's pass phrase.";
    }

    private void Share_Click(object? sender, RoutedEventArgs e) => _shell.Run(ShareAsync);

    private async Task ShareAsync()
    {
        var services = _shell.Services;
        if (services.Server.Client is not { } client)
        {
            return;
        }

        var email = await _shell.AskText("Share with: their email", string.Empty, "Next", text => null, text => text.Contains('@'));
        if (email is null)
        {
            return;
        }

        (Lookup Result, ShareCandidate? Candidate) found;
        try
        {
            found = await Busily(() => VaultSharing.FindAsync(client, email));
        }
        catch (Kofrenot.Core.Server.ServerException ex)
        {
            services.Log.Warn("Action", $"Cannot find an account to share with ({ex.Failure})");
            _shell.Tell($"The account cannot be looked up.\n\n{ex.Message}");
            return;
        }

        var (result, candidate) = found;
        if (candidate is null)
        {
            _shell.Tell(result == Lookup.NoAccount ? $"No account of this server has the email {email.Trim()}." : "This account cannot be shared with yet: it has no pass phrase. They choose it in kofrenot on Windows first.");
            return;
        }

        var role = RoleBox.SelectedIndex == 1 ? MemberRole.Reader : MemberRole.Writer;
        if (!await _shell.Confirm($"Share this vault with {candidate.Email} as a {role.ToString().ToLowerInvariant()}?\n\nCompare this fingerprint with theirs first:\n{candidate.Fingerprint}", "Share"))
        {
            services.Log.Info("Action", "Sharing cancelled");
            return;
        }

        var answers = new PreparedAnswers { Share = new ShareChoice(candidate, role) };
        await Busily(() => services.Session.Share(answers));
        _shell.Tell(answers.Told);
        Refresh();
    }

    private void Member_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is VaultMember { Role: not MemberRole.Owner } member && _shell.Services.Session.CanManageMembers)
        {
            _shell.Run(() => ChangeRoleAsync(member));
        }
    }

    private async Task ChangeRoleAsync(VaultMember member)
    {
        var role = member.Role == MemberRole.Writer ? "a reader" : "a writer";
        if (!await _shell.Confirm($"Make {member.Email} {role}?", member.Role == MemberRole.Writer ? "Make reader" : "Make writer"))
        {
            _shell.Services.Log.Info("Action", "Role change cancelled");
            return;
        }

        var answers = new PreparedAnswers { Confirmed = true };
        await Busily(() => _shell.Services.Session.ChangeRole(answers, member));
        _shell.Tell(answers.Told);
        Refresh();
    }

    private void Leave_Click(object? sender, RoutedEventArgs e) => _shell.Run(LeaveAsync);

    private async Task LeaveAsync()
    {
        if (!await _shell.Confirm("Leave this vault?\n\nIt goes out of your list, and you can no longer reach it on the server. Its owner can share it with you again.", "Leave"))
        {
            _shell.Services.Log.Info("Action", "Leaving cancelled");
            return;
        }

        var answers = new PreparedAnswers { Confirmed = true };
        await Busily(() => _shell.Services.Session.Leave(answers));
        _shell.Tell(answers.Told);
        _shell.Start();
    }

    private async Task<T> Busily<T>(Func<Task<T>> work)
    {
        Busy.IsVisible = true;
        try
        {
            return await work();
        }
        finally
        {
            Busy.IsVisible = false;
        }
    }

    private Task Busily(Func<Task> work) => Busily(async () =>
    {
        await work();
        return true;
    });
}
