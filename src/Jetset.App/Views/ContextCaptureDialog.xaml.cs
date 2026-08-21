using System.Windows;
using System.Windows.Input;
using Jetset.App.Models;
using Jetset.App.ViewModels;

namespace Jetset.App.Views;

public partial class ContextCaptureDialog : Window
{
    public ContextCaptureDialog(ContextCaptureRequest request)
    {
        InitializeComponent();
        ViewModel = new ContextCaptureViewModel(request);
        DataContext = ViewModel;
        CaptureResult = ContextCaptureResult.Skipped;
        SkipButton.IsCancel = ViewModel.SkipIsCancel;
        CancelButton.IsCancel = ViewModel.ShowCancel;
        Loaded += (_, _) => FocusInitialField();
    }

    public ContextCaptureViewModel ViewModel { get; }

    public ContextCaptureResult CaptureResult { get; private set; }

    public WorkingContext Context => ViewModel.ToWorkingContext();

    public string? SessionNote =>
        string.IsNullOrWhiteSpace(ViewModel.SessionNote) ? null : ViewModel.SessionNote.Trim();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        CaptureResult = ContextCaptureResult.Saved;
        DialogResult = true;
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        CaptureResult = ContextCaptureResult.Skipped;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        CaptureResult = ContextCaptureResult.Cancelled;
        DialogResult = false;
    }

    private void FocusInitialField()
    {
        var box = ViewModel.Reason == ContextCaptureReason.Finish
            ? LastProgressBox
            : CurrentStatusBox;
        Keyboard.Focus(box);
        box.CaretIndex = box.Text.Length;
    }
}
