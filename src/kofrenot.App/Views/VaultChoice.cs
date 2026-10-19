using System.ComponentModel;
using Kofrenot.Core.Settings;

namespace Kofrenot.UI.Views;

/// <summary>A registered vault in the main window's vault box: its name, and whether its folder can be reached.</summary>
public sealed class VaultChoice(RegisteredVault vault, string name) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public RegisteredVault Vault => vault;

    public string Folder => vault.Location.Folder;

    /// <summary>False once its folder was found out of reach (a file share offline, a folder gone).</summary>
    public bool IsAvailable { get; private set; } = true;

    public string Text => IsAvailable ? name : $"{name} (unavailable)";

    public void SetAvailable(bool available)
    {
        if (available == IsAvailable)
        {
            return;
        }

        IsAvailable = available;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAvailable)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
    }
}
