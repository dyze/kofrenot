using System.Windows.Threading;
using Kofrenot.Core.Session;

namespace Kofrenot.UI.Platform;

/// <summary>The WPF UI thread: the dispatcher of the thread that made it.</summary>
public sealed class DispatcherUiThread : IUiThread
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

    public void Post(Action action) => _dispatcher.BeginInvoke(action);
}
