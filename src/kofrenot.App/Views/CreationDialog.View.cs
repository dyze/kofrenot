using System.Windows;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>A new view's step: its definition, in a <see cref="ViewDefinitionEditor"/>.</summary>
public partial class CreationDialog
{
    private void ShowView(Side side) =>
        ViewEditor.Visibility = _form.IsView(side) ? Visibility.Visible : Visibility.Collapsed;

    private void ViewEditor_Changed()
    {
        if (StepSide is { } side)
        {
            _form.SetView(side, ViewEditor.Draft);
            ShowForm();
        }
    }
}
