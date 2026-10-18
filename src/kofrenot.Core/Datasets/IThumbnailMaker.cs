namespace Kofrenot.Core.Datasets;

/// <summary>Makes the thumbnail of an image: decoding images needs Windows, so the App implements it.</summary>
public interface IThumbnailMaker
{
    /// <summary>The longest side of a thumbnail, in pixels.</summary>
    const int Size = 128;

    /// <summary>A PNG of the image, <see cref="Size"/> pixels at most on its longest side; null when it cannot be decoded.</summary>
    byte[]? Make(Stream image);
}
