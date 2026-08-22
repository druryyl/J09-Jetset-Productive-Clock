using Jetset.App.ViewModels;

namespace Jetset.App.Views;

public partial class CompactOverlayView : System.Windows.Controls.UserControl
{
    public CompactOverlayView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
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
        QuickCaptureTextBox.Focus();
        QuickCaptureTextBox.SelectAll();
    }
}
