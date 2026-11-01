using Avalonia.Controls;
using Avalonia.Media;
using Kofrenot.Core.Browse;

namespace Kofrenot.Phone.Views;

/// <summary>
/// kofrenot's icons (<see cref="IconPaths"/>), the same as on Windows, as resources <c>icon.{name}</c>
/// (<c>{StaticResource icon.delete}</c>, <see cref="PhoneTheme.Icon"/>), each parsed once at start.
/// </summary>
public static class IconResources
{
    public static string KeyOf(string name) => $"icon.{name}";

    public static void AddTo(IResourceDictionary resources)
    {
        foreach (var name in IconPaths.Names)
        {
            resources[KeyOf(name)] = StreamGeometry.Parse(IconPaths.PathOf(name));
        }
    }
}
