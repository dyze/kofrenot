using Kofrenot.Core.Logging;
using Kofrenot.Core.Session;
using Kofrenot.Core.Settings;

namespace Kofrenot.Core.Tests.Session;

public sealed class PrivacyGuardTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly SettingsStore _settings;
    private readonly RingLog _log = new();
    private readonly PrivacyGuard _guard;

    public PrivacyGuardTests()
    {
        _settings = new SettingsStore(_folder.Sub("settings.json"));
        _guard = new PrivacyGuard(_settings, _log);
    }

    private static TimeSpan Minutes(double minutes) => TimeSpan.FromMinutes(minutes);

    [Fact]
    public void ByDefault_TheVaultShown_IsCoveredAfterFiveMinutesWithoutInput_NotLocked()
    {
        Assert.Equal(PrivacyStep.None, _guard.Next(Minutes(4.9), vaultOpen: true, vaultShown: true));
        Assert.Equal(PrivacyStep.Cover, _guard.Next(Minutes(5), vaultOpen: true, vaultShown: true));
        Assert.Equal(PrivacyStep.Cover, _guard.Next(TimeSpan.FromDays(1), vaultOpen: true, vaultShown: true));
    }

    [Fact]
    public void AVaultNotShown_IsNotCovered()
    {
        Assert.Equal(PrivacyStep.None, _guard.Next(Minutes(10), vaultOpen: true, vaultShown: false));
    }

    [Fact]
    public void NoCover_WhenSwitchedOff()
    {
        _settings.SetCoverAfterMinutes(0);

        Assert.Equal(PrivacyStep.None, _guard.Next(TimeSpan.FromDays(1), vaultOpen: true, vaultShown: true));
    }

    [Fact]
    public void TheLockAfterIdle_ComesBeforeTheCover_AlsoForAVaultNotShown()
    {
        _settings.SetLockAfterMinutes(15);

        Assert.Equal(PrivacyStep.Cover, _guard.Next(Minutes(14), vaultOpen: true, vaultShown: true));
        Assert.Equal(PrivacyStep.Lock, _guard.Next(Minutes(15), vaultOpen: true, vaultShown: true));
        Assert.Equal(PrivacyStep.Lock, _guard.Next(Minutes(15), vaultOpen: true, vaultShown: false));
    }

    [Fact]
    public void ByDefault_WindowsLocking_HidesTheVaultOnce_ShownOrNot()
    {
        _guard.WindowsLocking("session locked");

        Assert.Equal(PrivacyStep.Hide, _guard.Next(TimeSpan.Zero, vaultOpen: true, vaultShown: false));
        Assert.Equal(PrivacyStep.None, _guard.Next(TimeSpan.Zero, vaultOpen: true, vaultShown: true));
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Debug && entry.Message == "Hide due: session locked");
    }

    [Fact]
    public void AHideDue_IsForgotten_WhenTheVaultCloses()
    {
        _guard.WindowsLocking("sleep");

        Assert.Equal(PrivacyStep.None, _guard.Next(TimeSpan.Zero, vaultOpen: false, vaultShown: false));
        Assert.Equal(PrivacyStep.None, _guard.Next(TimeSpan.Zero, vaultOpen: true, vaultShown: true));
    }

    [Fact]
    public void ALock_ComesBeforeAHide()
    {
        _settings.SetLockAfterMinutes(10);
        _guard.WindowsLocking("session locked");

        Assert.Equal(PrivacyStep.Lock, _guard.Next(Minutes(10), vaultOpen: true, vaultShown: true));
        Assert.Equal(PrivacyStep.None, _guard.Next(TimeSpan.Zero, vaultOpen: true, vaultShown: true));
    }

    [Fact]
    public void WindowsLocking_MakesTheLockDue_UntilTheVaultIsClosed_WhenSetToLock()
    {
        _settings.SetOnWindowsLock(WindowsLockAction.Lock);
        _guard.WindowsLocking("session locked");

        Assert.Equal(PrivacyStep.Lock, _guard.Next(TimeSpan.Zero, vaultOpen: true, vaultShown: false));
        Assert.Equal(PrivacyStep.Lock, _guard.Next(TimeSpan.Zero, vaultOpen: true, vaultShown: true));
        Assert.Equal(PrivacyStep.None, _guard.Next(TimeSpan.Zero, vaultOpen: false, vaultShown: false));
        Assert.Equal(PrivacyStep.None, _guard.Next(TimeSpan.Zero, vaultOpen: true, vaultShown: true));
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Debug && entry.Message.Contains("session locked"));
    }

    [Fact]
    public void WindowsLocking_DoesNothing_WhenSwitchedOff()
    {
        _settings.SetOnWindowsLock(WindowsLockAction.Nothing);

        _guard.WindowsLocking("sleep");

        Assert.Equal(PrivacyStep.None, _guard.Next(TimeSpan.Zero, vaultOpen: true, vaultShown: true));
    }

    [Fact]
    public void WithNoVaultOpen_NothingIsDone()
    {
        _settings.SetLockAfterMinutes(1);

        Assert.Equal(PrivacyStep.None, _guard.Next(TimeSpan.FromDays(1), vaultOpen: false, vaultShown: false));
    }

    [Fact]
    public void ALockThatWaits_IsToldOnce_ThenAgainForTheNextLock()
    {
        _settings.SetOnWindowsLock(WindowsLockAction.Lock);
        _guard.WindowsLocking("session locked");
        _guard.Next(TimeSpan.Zero, vaultOpen: true, vaultShown: true);
        _guard.LockWaits("a note is not saved");
        _guard.LockWaits("a note is not saved");
        Assert.Single(_log.Entries, entry => entry.Level == LogLevel.Warning);

        _guard.Next(TimeSpan.Zero, vaultOpen: false, vaultShown: false);
        _guard.WindowsLocking("sleep");
        _guard.LockWaits("a file is being imported");

        Assert.Equal(2, _log.Entries.Count(entry => entry.Level == LogLevel.Warning));
    }

    [Fact]
    public void ALockNoLongerDue_ForgetsItWaited()
    {
        _settings.SetLockAfterMinutes(10);
        _guard.Next(Minutes(10), vaultOpen: true, vaultShown: true);
        _guard.LockWaits("a note is not saved");

        Assert.Equal(PrivacyStep.Cover, _guard.Next(Minutes(6), vaultOpen: true, vaultShown: true));
        _guard.LockWaits("a note is not saved");

        Assert.Equal(2, _log.Entries.Count(entry => entry.Level == LogLevel.Warning));
    }

    public void Dispose() => _folder.Dispose();
}
