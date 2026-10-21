using System.Text.Json;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;

namespace Kofrenot.Core.Server;

/// <summary>
/// The session on each server, protected by <see cref="ISecretProtector"/>, so kofrenot stays
/// signed in across restarts without keeping the password. A missing or damaged file means
/// signed out.
/// </summary>
public sealed class ServerSessionMemory(string path, ISecretProtector protector, ILog? log = null)
{
    private readonly ProtectedStore _store = new(path, protector, "server session", log ?? NullLog.Instance);

    /// <summary>The session kept for <paramref name="server"/>; null when none can be read.</summary>
    public ServerSession? Recall(Uri server)
    {
        try
        {
            return _store.Recall(server.ToString()) is { } json ? JsonSerializer.Deserialize<ServerSession>(json) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Keeps <paramref name="session"/>; null forgets it (signed out).</summary>
    public void Remember(Uri server, ServerSession? session)
    {
        if (session is null)
        {
            _store.Forget(server.ToString());
        }
        else
        {
            _store.Remember(server.ToString(), JsonSerializer.SerializeToUtf8Bytes(session));
        }
    }
}
