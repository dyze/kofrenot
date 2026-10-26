using Kofrenot.Core.Session;

namespace Kofrenot.Phone.Platform;

/// <summary>
/// No folder vault on the phone: a server vault's changes are read after each sync
/// (<see cref="VaultSession"/>), so no folder is watched.
/// </summary>
public sealed class NothingToWatch : IVaultWatcher
{
    public IDisposable Watch(string folder, Action changed) => new Nothing();

    private sealed class Nothing : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
