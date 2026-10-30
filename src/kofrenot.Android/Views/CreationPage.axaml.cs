using Avalonia.Controls;
using Avalonia.Interactivity;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Phone.Platform;

namespace Kofrenot.Phone.Views;

/// <summary>
/// The phone's creation screen for what the search box's text adds (<see cref="CreationForm"/>):
/// the link's type, each new node's type (a view or a dataset is made elsewhere: on Windows, or a
/// file added with ＋) and a new note's text. Add adds it and goes back, as on Windows: its nodes first
/// among the recent ones, the search box cleared (<paramref name="added"/>); back cancels.
/// </summary>
public sealed partial class CreationPage : UserControl, IPage
{
    private readonly Shell _shell;
    private readonly CreationForm _form;
    private readonly Action _added;

    public CreationPage(Shell shell, CreationForm form, Action added)
    {
        _shell = shell;
        _form = form;
        _added = added;
        InitializeComponent();
        Heading.Text = form.Title;
        ShowSides();
        ShowStatus();
    }

    public bool GoBack() => false;

    public void Refresh()
    {
    }

    public void SyncChanged()
    {
    }

    private IReadOnlyList<NodeType> Types =>
        _form.Types.NodeTypes.Where(type => type != TypeCatalog.View && type != TypeCatalog.Dataset).ToList();

    private void ShowSides()
    {
        Sides.Children.Clear();
        if (_form.IsLink)
        {
            Sides.Children.Add(new TextBlock { Classes = { "caption" }, Text = "Link" });
            var verbs = _form.Types.Verbs.ToList();
            var verb = new ComboBox { ItemsSource = verbs.Select(VerbName).ToList(), SelectedIndex = verbs.IndexOf(_form.Verb!), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
            verb.SelectionChanged += (_, _) =>
            {
                _form.Verb = verbs[verb.SelectedIndex];
                ShowStatus();
            };
            Sides.Children.Add(verb);
        }

        foreach (var side in _form.Steps.Select(step => step.Side).OfType<Side>())
        {
            ShowSide(side);
        }
    }

    // A new node: its name, its type, and for a note its text.
    private void ShowSide(Side side)
    {
        Sides.Children.Add(new TextBlock { Classes = { "caption" }, Text = $"New node: {_form.NameOf(side)}", Margin = new Avalonia.Thickness(0, 8, 0, 0) });
        var types = Types;
        var type = new ComboBox { ItemsSource = types.Select(nodeType => nodeType.Name).ToList(), SelectedIndex = Math.Max(0, types.ToList().IndexOf(_form.TypeOf(side))), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        var text = new TextBox { AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinHeight = 120, PlaceholderText = "Its text: Markdown, - [ ] task", Text = _form.ContentOf(side), IsVisible = _form.IsNote(side) };
        type.SelectionChanged += (_, _) =>
        {
            _form.SetType(side, types[type.SelectedIndex]);
            text.IsVisible = _form.IsNote(side);
            ShowStatus();
        };
        text.TextChanged += (_, _) => _form.SetContent(side, text.Text ?? string.Empty);
        Sides.Children.Add(type);
        Sides.Children.Add(text);
    }

    private static string VerbName(LinkVerb verb) => verb.Phrases[0];

    private void ShowStatus()
    {
        Status.Text = _form.Status;
        Warning.Text = _form.Warning;
        Warning.IsVisible = _form.Warning is not null;
        AddButton.IsEnabled = _form.CanAdd;
    }

    private void Add_Click(object? sender, RoutedEventArgs e)
    {
        var answers = new PreparedAnswers();
        var plan = _shell.Services.Session.Add(answers, _form);
        _shell.Tell(answers.Told);
        if (plan is null || _shell.Services.Session.Current is not { } vault)
        {
            return;
        }

        // The first typed on top.
        foreach (var node in plan.Nodes.Reverse())
        {
            _shell.Services.Recent.Opened(vault.Id, node.Id);
        }

        _added();
        _shell.Back();
    }

    private void Back_Click(object? sender, RoutedEventArgs e)
    {
        _shell.Services.Log.Info("Action", "Creation cancelled");
        _shell.Back();
    }
}
