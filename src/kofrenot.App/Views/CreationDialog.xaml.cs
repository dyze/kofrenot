using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>
/// The creation screen, shown by <see cref="DatabaseSession.Create"/> from anywhere: a view of a
/// <see cref="CreationForm"/>, whose type choices it passes on.
/// </summary>
public partial class CreationDialog : Window
{
    private readonly CreationForm _form;
    private bool _showing;

    private CreationDialog(Window owner, CreationForm form)
    {
        InitializeComponent();
        Owner = owner;
        _form = form;

        TitleText.Text = form.Title;
        LeftTypeBox.ItemsSource = RightTypeBox.ItemsSource = form.Types.NodeTypes;
        LinkTypeBox.ItemsSource = form.Types.LinkTypes;
        foreach (var element in new FrameworkElement[] { LinkLabel, LinkTypeBox, RightPanel, RightTypeBox })
        {
            element.Visibility = form.IsLink ? Visibility.Visible : Visibility.Collapsed;
        }

        ShowForm();
        Loaded += (_, _) => AddButton.Focus();
    }

    /// <summary>Shows the screen: the plan to add, or null when cancelled.</summary>
    public static AddPlan? Ask(Window owner, CreationForm form)
    {
        var dialog = new CreationDialog(owner, form);
        return dialog.ShowDialog() == true ? form.Plan : null;
    }

    private void ShowForm()
    {
        _showing = true;
        LeftTypeBox.SelectedItem = _form.LeftType;
        RightTypeBox.SelectedItem = _form.RightType;
        LinkTypeBox.SelectedItem = _form.LinkType;

        if (_form.Plan is LinkPlan link)
        {
            ShowSide(link.Left, LeftName, LeftInfo, LeftTypeBox);
            if (link.Right is { } right)
            {
                ShowSide(right, RightName, RightInfo, RightTypeBox);
            }
        }
        else if (_form.Plan is NodePlan node)
        {
            LeftName.Text = node.Name;
            LeftInfo.Text = "New node";
        }

        WarningText.Text = _form.Warning ?? string.Empty;
        WarningText.Visibility = _form.Warning is null ? Visibility.Collapsed : Visibility.Visible;
        ExistingList.ItemsSource = _form.Existing;
        StatusText.Text = _form.Status;
        AddButton.IsEnabled = _form.Plan.CanAdd;
        _showing = false;
    }

    // An existing node keeps its type: the box is only for a new one.
    private void ShowSide(PlannedSide side, TextBlock name, TextBlock info, ComboBox type)
    {
        name.Text = _form.Label(side);
        info.Text = _form.Describe(side);
        type.Visibility = side.IsNew ? Visibility.Visible : Visibility.Hidden;
    }

    private void LeftTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        Choose<NodeType>(LeftTypeBox, type => _form.LeftType = type);

    private void RightTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        Choose<NodeType>(RightTypeBox, type => _form.RightType = type);

    private void LinkTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        Choose<LinkType>(LinkTypeBox, type => _form.LinkType = type);

    private void Choose<T>(ComboBox box, Action<T> apply)
        where T : class
    {
        if (_showing || box.SelectedItem is not T type)
        {
            return;
        }

        apply(type);
        ShowForm();
    }

    private void AddButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindows.RemoveMinimizeMaximizeButtons(new WindowInteropHelper(this).Handle);
}
