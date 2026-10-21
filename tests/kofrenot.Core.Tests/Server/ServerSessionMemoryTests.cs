namespace Kofrenot.Core.Tests.Server;

public sealed class ServerSessionMemoryTests : IDisposable
{
    private static readonly Uri Server = new("https://kofre.example/");

    private readonly TempFolder _temp = new();
    private readonly ServerSession _session = new("access", DateTimeOffset.UnixEpoch, "refresh-token");

    private string FilePath => _temp.Sub("server-session.json");

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void ASession_IsRecalled_ForItsServerOnly_AndKeptProtected()
    {
        Memory().Remember(Server, _session);

        Assert.Equal(_session, Memory().Recall(Server));
        Assert.Null(Memory().Recall(new Uri("https://other.example/")));
        Assert.DoesNotContain("refresh-token", File.ReadAllText(FilePath));
    }

    [Fact]
    public void SigningOut_ForgetsIt()
    {
        Memory().Remember(Server, _session);

        Memory().Remember(Server, null);

        Assert.Null(Memory().Recall(Server));
    }

    [Fact]
    public void ASessionItCannotRead_IsNone()
    {
        var memory = new ServerSessionMemory(FilePath, new ReversingProtector());
        memory.Remember(Server, _session);

        Assert.Null(new ServerSessionMemory(FilePath, new GarblingProtector()).Recall(Server));
    }

    private ServerSessionMemory Memory() => new(FilePath, new ReversingProtector());

    private sealed class ReversingProtector : ISecretProtector
    {
        public byte[] Protect(byte[] secret) => [.. secret.Reverse()];

        public byte[] Unprotect(byte[] protectedSecret) => [.. protectedSecret.Reverse()];
    }

    /// <summary>Unprotects into something that is not a session.</summary>
    private sealed class GarblingProtector : ISecretProtector
    {
        public byte[] Protect(byte[] secret) => secret;

        public byte[] Unprotect(byte[] protectedSecret) => "{not json"u8.ToArray();
    }
}
