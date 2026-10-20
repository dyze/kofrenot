using System.Windows;

namespace Kofrenot.UI.Platform;

/// <summary>
/// What a drag inside kofrenot carries (<paramref name="ownFormat"/>: <paramref name="data"/>), offering a file too, made only
/// when it is asked for: when the drag is over another program that takes files (Explorer). A drop
/// inside kofrenot never asks for it, so nothing is decrypted.
/// </summary>
/// <param name="makeFile">Writes the file, then gives its path; called once.</param>
internal sealed class LazyFileDrop(string ownFormat, object data, Func<string> makeFile) : IDataObject
{
    private string? _path;

    /// <summary>True once the file was made: it was dragged out of kofrenot.</summary>
    public bool WasMade => _path is not null;

    public object? GetData(string format) => format switch
    {
        _ when format == ownFormat => data,
        _ when format == DataFormats.FileDrop => new[] { _path ??= makeFile() },
        _ => null,
    };

    public object? GetData(string format, bool autoConvert) => GetData(format);

    public object? GetData(Type format) => GetData(format.FullName ?? string.Empty);

    public bool GetDataPresent(string format) => format == ownFormat || format == DataFormats.FileDrop;

    public bool GetDataPresent(string format, bool autoConvert) => GetDataPresent(format);

    public bool GetDataPresent(Type format) => GetDataPresent(format.FullName ?? string.Empty);

    public string[] GetFormats() => [ownFormat, DataFormats.FileDrop];

    public string[] GetFormats(bool autoConvert) => GetFormats();

    public void SetData(object data) => throw new NotSupportedException();

    public void SetData(string format, object data) => throw new NotSupportedException();

    public void SetData(Type format, object data) => throw new NotSupportedException();

    public void SetData(string format, object data, bool autoConvert) => throw new NotSupportedException();
}
