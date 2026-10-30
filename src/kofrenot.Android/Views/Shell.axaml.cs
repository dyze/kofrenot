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
    /// <summary>Handles Back within the screen (a search cleared, a note not saved asked about); false to show the screen before.</summary>
    bool GoBack();

    void Refresh();

    /// <summary>A sync ended (every 30 s): only where it stands is shown again, what it downloaded comes with <see cref="Refresh"/>.</summary>
    void SyncChanged();
}

/// <summary>
/// The app's one window: its screens, one back stack (Back and ← go to the screen before), and
/// the messages and questions shown over them. Opens the vault, asking its pass phrase on its own
/// screen first.
/// </summary>
public sealed partial class Shell : UserControl
{
    private readonly Stack<Control> _pages = new();
    private readonly Queue<Message> _messages = new();
    private Message? _shown;

    public Shell(AppServices services)
    {
        Services = services;
        // Here, not when the app starts: the services are built once the activity shows its view.
        PhoneTheme.Apply(services.Settings.Theme);
        InitializeComponent();
        AddHandler(TopBar.BackEvent, (_, e) =>
        {
            GoBack();
            e.Handled = true;
        });
        services.Session.Changed += RefreshPage;
        services.Sync.Changed += () => (PageHost.Content as IPage)?.SyncChanged();
        services.Server.Changed += RefreshPage;
        Start();
        AddShared();
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
            AddShared();
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
        ShowPage(page, back: false);
    }

    private void ShowPage(Control page, bool back)
    {
        ThumbnailCache.Shared.Use(Services.Session.Current);
        PageHost.IsTransitionReversed = back;
        PageHost.Content = page;
    }

    public void Tell(string message) => Enqueue(new Message(message, Yes: null, Answer: null));

    public void Tell(IEnumerable<string> messages)
    {
        foreach (var message in messages)
        {
            Tell(message);
        }
    }

    /// <summary>Asks to confirm; true for <paramref name="yes"/>.</summary>
    public async Task<bool> Confirm(string question, string yes) => await Ask(new Message(question, yes, new())) is not null;

    /// <summary>
    /// Asks a text, <paramref name="typed"/> to begin with; null when cancelled.
    /// <paramref name="remark"/> says what is wrong with a text, or worth a warning (null when nothing is);
    /// <paramref name="isFine"/> tells whether <paramref name="yes"/> takes it.
    /// </summary>
    public Task<string?> AskText(string question, string typed, string yes, Func<string, string?> remark, Func<string, bool> isFine) =>
        Ask(new Message(question, yes, new(), typed, remark, isFine));

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
            WatchInsets(top);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is { } top)
        {
            top.BackRequested -= OnBackRequested;
            UnwatchInsets(top);
        }

        base.OnDetachedFromVisualTree(e);
    }

    // Android's Back and ← of the top bar do the same; at the first screen, Back is Android's own (home).
    private void OnBackRequested(object? sender, RoutedEventArgs e) => e.Handled = GoBack();

    // The message shown, else the sheet, else within the screen, else the screen before.
    private bool GoBack()
    {
        if (Overlay.IsVisible)
        {
            Answer(yes: false);
            return true;
        }

        if (IsSheetShown)
        {
            CloseSheet();
            return true;
        }

        return PageHost.Content is IPage page && page.GoBack() || Back();
    }

    /// <summary>Whether there is a screen before the one shown.</summary>
    public bool CanGoBack => _pages.Count > 1;

    public bool IsShown(Control page) => _pages.TryPeek(out var shown) && shown == page;

    /// <summary>The screens from the first to <paramref name="page"/>, it included (shown or about to be).</summary>
    public IEnumerable<Control> PagesUpTo(Control page) => _pages.Reverse().TakeWhile(before => before != page).Append(page);

    /// <summary>The home, with <paramref name="page"/> over it, as a deep link opens: Back goes to the home.</summary>
    public void ShowOverHome(Control page)
    {
        if (_pages.LastOrDefault() is not HomePage)
        {
            Show(new HomePage(this));
        }

        while (_pages.Count > 1)
        {
            _pages.Pop();
        }

        Push(page);
    }

    /// <summary>Shows the screen before; false at the first one.</summary>
    public bool Back()
    {
        if (_pages.Count <= 1)
        {
            return false;
        }

        _pages.Pop();
        ShowPage(_pages.Peek(), back: true);
        RefreshPage();
        return true;
    }

    // The vault may have changed: the thumbnails are those of the one open.
    private void RefreshPage()
    {
        ThumbnailCache.Shared.Use(Services.Session.Current);
        (PageHost.Content as IPage)?.Refresh();
    }

    private Task<string?> Ask(Message message)
    {
        Enqueue(message);
        return message.Answer!.Task;
    }

    private void Enqueue(Message message)
    {
        _messages.Enqueue(message);
        if (!Overlay.IsVisible)
        {
            ShowNext();
        }
    }

    private void ShowNext()
    {
        if (!_messages.TryDequeue(out var message))
        {
            _shown = null;
            Overlay.IsVisible = false;
            return;
        }

        _shown = message;
        OverlayText.Text = message.Text;
        OverlayYes.Content = message.Yes ?? "OK";
        OverlayNo.IsVisible = message.Yes is not null;
        OverlayBox.IsVisible = message.Typed is not null;
        OverlayBox.Text = message.Typed ?? string.Empty;
        ShowRemark();
        Overlay.IsVisible = true;
        if (message.Typed is not null)
        {
            OverlayBox.Focus();
            OverlayBox.SelectAll();
        }
    }

    // What is wrong with the text typed, under it; OK only once it is fine.
    private void ShowRemark()
    {
        var text = OverlayBox.Text ?? string.Empty;
        var remark = _shown?.Remark?.Invoke(text);
        OverlayRemark.Text = remark;
        OverlayRemark.IsVisible = _shown?.Typed is not null && remark is not null;
        OverlayYes.IsEnabled = _shown?.IsFine?.Invoke(text) ?? true;
    }

    private void Answer(bool yes)
    {
        var shown = _shown;
        var text = shown?.Typed is null ? string.Empty : OverlayBox.Text ?? string.Empty;
        ShowNext();
        shown?.Answer?.SetResult(yes ? text : null);
    }

    private void OverlayBox_TextChanged(object? sender, TextChangedEventArgs e) => ShowRemark();

    private void OverlayYes_Click(object? sender, RoutedEventArgs e) => Answer(yes: true);

    private void OverlayNo_Click(object? sender, RoutedEventArgs e) => Answer(yes: false);

    /// <summary>A message, a question (<see cref="Yes"/>, answered by <see cref="Answer"/>: null when cancelled), or a text asked (<see cref="Typed"/>).</summary>
    private sealed record Message(
        string Text,
        string? Yes,
        TaskCompletionSource<string?>? Answer,
        string? Typed = null,
        Func<string, string?>? Remark = null,
        Func<string, bool>? IsFine = null);
}
