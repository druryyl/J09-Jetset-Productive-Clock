using System.Windows;
using Jetset.App.ViewModels;

namespace Jetset.App.Views;

public partial class FocusView : System.Windows.Controls.UserControl
{
    public FocusView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is FocusViewModel oldVm)
        {
            oldVm.QuickCaptureFocusRequested -= OnQuickCaptureFocusRequested;
        }

        if (e.NewValue is FocusViewModel newVm)
        {
            newVm.QuickCaptureFocusRequested += OnQuickCaptureFocusRequested;
        }
    }

    private void OnQuickCaptureFocusRequested(object? sender, EventArgs e)
    {
        var textBox = IsCompactQuickCaptureVisible() ? CompactQuickCaptureTextBox : QuickCaptureTextBox;
        textBox.Focus();
        textBox.SelectAll();
    }

    private bool IsCompactQuickCaptureVisible() =>
        DataContext is FocusViewModel vm && vm.IsCompact;
}
