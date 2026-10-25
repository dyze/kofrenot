using Avalonia.Threading;
using Kofrenot.Core.Session;

namespace Kofrenot.Phone.Platform;

/// <summary>The UI thread: Avalonia's dispatcher.</summary>
public sealed class AvaloniaUiThread : IUiThread
{
    public void Post(Action action) => Dispatcher.UIThread.Post(action);
}
