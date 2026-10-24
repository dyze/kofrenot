namespace Kofrenot.Core.Settings;

/// <summary>A shortcut and what it does, where it works.</summary>
public sealed record KeyBinding(string Area, string Keys, string Action);

/// <summary>
/// Every shortcut of kofrenot, by area, as Settings > Key bindings lists them. Written by hand:
/// update it with the key handlers. The global hotkeys come from the settings.
/// </summary>
public static class KeyBindings
{
    public const string Anywhere = "Anywhere in Windows";
    public const string MainWindow = "Main window";
    public const string InputBox = "Input box";
    public const string NodeDetails = "Node details";
    public const string Notes = "Notes";
    public const string Datasets = "Datasets";
    public const string Views = "Views";
    public const string CreationScreen = "Creation screen";
    public const string ImageWindow = "Image window";
    public const string Settings = "Settings";

    private static readonly KeyBinding[] Fixed =
    [
        new(MainWindow, "Esc", "Hide the window (kofrenot keeps running in the notification area)"),
        new(MainWindow, "Ctrl+Tab, Ctrl+Shift+Tab", "Next, previous tab"),
        new(MainWindow, "Ctrl+1 … Ctrl+9", "Switch to the vault at that place in the list"),
        new(MainWindow, "Ctrl+L", "Lock the vault"),
        new(InputBox, "Enter", "Add what is typed, or open the details of the node selected"),
        new(InputBox, "Up, Down", "Select a node, or choose among nodes of the same name"),
        new(InputBox, "Tab, Right", "Accept the proposal shown in grey (Tab without one, while a link is typed: the other side)"),
        new(InputBox, "Backspace", "In an empty text: the last label becomes text again"),
        new(InputBox, "Esc", "Hide the proposal, clear the box, then hide the window"),
        new(NodeDetails, "Up, Down", "Select a link"),
        new(NodeDetails, "Enter", "Open the node at the other end of the link selected"),
        new(NodeDetails, "F2", "Rename the node"),
        new(NodeDetails, "Ctrl+K", "Link the node to another one"),
        new(NodeDetails, "Esc", "Back one level"),
        new(Notes, "Ctrl+S", "Save the note"),
        new(Notes, "Esc", "Leave the note (it is saved)"),
        new(Notes, "Enter", "In a task list: add an item after this one"),
        new(Notes, "Backspace", "In an empty item of a task list: remove it"),
        new(Notes, "Up, Down", "In a task list: previous, next item"),
        new(Notes, "Alt+Up, Alt+Down", "In a task list: move the item up, down"),
        new(Datasets, "Ctrl+I", "Import a file"),
        new(Datasets, "Ctrl+E", "Export the file"),
        new(Views, "Ctrl+F", "Find in the view"),
        new(Views, "Enter, Shift+Enter", "While finding: next, previous node found"),
        new(Views, "Enter", "Open the details of the node selected"),
        new(Views, "F2", "Rename the node selected"),
        new(Views, "Ctrl+C, Ctrl+X, Ctrl+V", "Copy, cut, paste a node (in a tree)"),
        new(Views, "Del", "Delete the node selected, with everything under it (in a tree)"),
        new(Views, "Esc", "Clear the search, or cancel a cut"),
        new(CreationScreen, "Ctrl+Enter", "Next step, or add"),
        new(CreationScreen, "Esc", "Cancel"),
        new(ImageWindow, "+, −, 0", "Zoom in, out, fit"),
        new(Settings, "Ctrl+F", "Find a setting"),
        new(Settings, "Ctrl+Tab", "Next tab"),
        new(Settings, "Esc", "Close Settings"),
    ];

    /// <summary>The areas, in the order they are listed.</summary>
    public static IReadOnlyList<string> Areas { get; } =
        [Anywhere, MainWindow, InputBox, NodeDetails, Notes, Datasets, Views, CreationScreen, ImageWindow, Settings];

    /// <summary>Every shortcut, by area: first the hotkey that shows kofrenot and each vault's, named by <paramref name="nameOf"/>.</summary>
    public static IReadOnlyList<KeyBinding> All(SettingsStore settings, Func<RegisteredVault, string> nameOf) =>
        settings.Vaults.Vaults
            .Where(vault => vault.Hotkey is not null)
            .Select(vault => new KeyBinding(Anywhere, vault.Hotkey!.ToString(), $"Show kofrenot on the vault “{nameOf(vault)}”"))
            .Prepend(new KeyBinding(Anywhere, settings.Hotkey.ToString(), "Show kofrenot"))
            .Concat(Fixed)
            .ToList();
}
