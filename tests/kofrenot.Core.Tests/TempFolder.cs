namespace Kofrenot.Core.Tests;

/// <summary>A folder for one test, deleted afterwards.</summary>
internal sealed class TempFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kofrenot-tests-" + Guid.NewGuid().ToString("N"));

    public string Sub(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
