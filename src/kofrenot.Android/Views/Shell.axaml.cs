using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Settings;
using Kofrenot.Phone.Platform;

namespace Kofrenot.Phone.Views;

/// <summary>A screen of the app: refreshed when the vault or the account changes; told when a sync ends.</summary>
public interface IPage
{
    /// <summary>Goes back within the screen (a trail of nodes); false when it has nowhere to go.</summary>
    bool GoBack();

    void Refresh();

    /// <summary>A sync ended (every 30 s): only where it stands is shown again, what it downloaded comes with <see cref="Refresh"/>.</summary>
    void SyncChanged();
}

/// <summary>
/// The app's one window: its screens (back goes to the one before), and the messages and
/// questions shown over them. Opens the vault, asking its pass phrase on its own screen first.
/// </summary>
public sealed partial class Shell : UserControl
{
    private readonly Stack<Control> _pages = new();
    private readonly Queue<(string Text, string? Yes, TaskCompletionSource<bool>? Answer)> _messages = new();
    private TaskCompletionSource<bool>? _answer;

    public Shell(AppServices services)
    {
        Services = services;
        InitializeComponent();
        services.Session.Changed += RefreshPage;
        services.Sync.Changed += () => (PageHost.Content as IPage)?.SyncChanged();
        services.Server.Changed += RefreshPage;
        Start();
    }

    public AppServices Services { get; }

    /// <summary>Signed out: the sign-in. Else the vault used last, or the list of vaults.</summary>
    public void Start()
    {
        if (Services.Server.Account is null)
        {
            Show(new SignInPage(this));
        }
        else if (Services.Session.Selected is { } selected)
        {
            Open(selected.Location);
        }
        else
        {
            Show(new VaultsPage(this));
        }
    }

    /// <summary>Opens a vault: with its pass phrase checked ahead (<see cref="PassPhrasePage"/>) or remembered; else asks it.</summary>
    public void Open(VaultLocation location)
    {
        var answers = new PreparedAnswers();
        Services.Session.SwitchTo(answers, location);
        AfterOpening(answers, location);
    }

    /// <summary>The vault open: its home. Its pass phrase needed: asked. Else, told why, the list of vaults.</summary>
    public void AfterOpening(PreparedAnswers answers, VaultLocation location)
    {
        Tell(answers.Told);
        if (Services.Session.Current is not null && Services.Session.Selected?.Location == location)
        {
            Show(new HomePage(this));
        }
        else if (answers.Needed is { } needed)
        {
            Show(new PassPhrasePage(this, location, needed));
        }
        else
        {
            Show(new VaultsPage(this));
        }
    }

    /// <summary>Shows a screen with nothing before it.</summary>
    public void Show(Control page)
    {
        _pages.Clear();
        Push(page);
    }

    /// <summary>Shows a screen, back going to the one shown now.</summary>
    public void Push(Control page)
    {
        _pages.Push(page);
        PageHost.Content = page;
    }

    public void Tell(string message) => Enqueue(message, yes: null, answer: null);

    public void Tell(IEnumerable<string> messages)
    {
        foreach (var message in messages)
        {
            Tell(message);
        }
    }

    /// <summary>Asks to confirm; true for <paramref name="yes"/>.</summary>
    public Task<bool> Confirm(string question, string yes)
    {
        var answer = new TaskCompletionSource<bool>();
        Enqueue(question, yes, answer);
        return answer.Task;
    }

    /// <summary>
    /// Runs what a tap started, its failures caught: an unexpected one is logged and told, not a
    /// crash. Failures the session expects it tells itself.
    /// </summary>
    public async void Run(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Failed(ex);
        }
    }

    /// <summary>An unexpected failure: logged, and told.</summary>
    public void Failed(Exception ex)
    {
        Services.Log.Error("App", $"Unexpected failure ({ex.GetType().Name})", ex);
        Tell($"Something went wrong.\n\n{ex.Message}");
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is { } top)
        {
            top.BackRequested += OnBackRequested;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is { } top)
        {
            top.BackRequested -= OnBackRequested;
        }

        base.OnDetachedFromVisualTree(e);
    }

    // Back: the message shown, else within the screen, else the screen before; at the first one, Android's own (home).
    private void OnBackRequested(object? sender, RoutedEventArgs e)
    {
        if (Overlay.IsVisible)
        {
            Answer(false);
            e.Handled = true;
        }
        else if (PageHost.Content is IPage page && page.GoBack())
        {
            e.Handled = true;
        }
        else
        {
            e.Handled = Back();
        }
    }

    /// <summary>Shows the screen before; false at the first one.</summary>
    public bool Back()
    {
        if (_pages.Count <= 1)
        {
            return false;
        }

        _pages.Pop();
        PageHost.Content = _pages.Peek();
        RefreshPage();
        return true;
    }

    private void RefreshPage() => (PageHost.Content as IPage)?.Refresh();

    private void Enqueue(string text, string? yes, TaskCompletionSource<bool>? answer)
    {
        _messages.Enqueue((text, yes, answer));
        if (!Overlay.IsVisible)
        {
            ShowNext();
        }
    }

    private void ShowNext()
    {
        if (!_messages.TryDequeue(out var message))
        {
            Overlay.IsVisible = false;
            return;
        }

        OverlayText.Text = message.Text;
        OverlayYes.Content = message.Yes ?? "OK";
        OverlayNo.IsVisible = message.Yes is not null;
        _answer = message.Answer;
        Overlay.IsVisible = true;
    }

    private void Answer(bool yes)
    {
        var answer = _answer;
        _answer = null;
        ShowNext();
        answer?.SetResult(yes);
    }

    private void OverlayYes_Click(object? sender, RoutedEventArgs e) => Answer(true);

    private void OverlayNo_Click(object? sender, RoutedEventArgs e) => Answer(false);
}
