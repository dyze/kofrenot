using System.Windows.Controls;

namespace Kofrenot.UI.Views;

/// <summary>The items of the right-click menus: a header, the keys that do the same, an action.</summary>
internal static class MenuItems
{
    public static MenuItem Of(string header, string keys, Action action, bool isEnabled = true)
    {
        var item = new MenuItem { Header = header, InputGestureText = keys, IsEnabled = isEnabled };
        item.Click += (_, _) => action();
        return item;
    }
}
