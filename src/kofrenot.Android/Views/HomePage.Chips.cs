namespace Kofrenot.Phone.Views;

/// <summary>
/// The search box (<see cref="ChipBox"/>): what it holds is searched, or added (Enter: its creation screen).
/// </summary>
public sealed partial class HomePage
{
    private Core.Input.ChipInput Chips => SearchBox.Input;

    private void WatchSearchBox()
    {
        SearchBox.GraphOf = () => Vault?.Graph;
        SearchBox.PlanOf = () => _adding?.Plan;
        SearchBox.Log = _shell.Services.Log;
        SearchBox.Submit = ShowCreation;
        SearchBox.Changed += (_, _) =>
        {
            if (Vault is { } vault)
            {
                ShowList(vault);
            }
        };
    }

    private void ClearSearch() => SearchBox.Clear();
}
