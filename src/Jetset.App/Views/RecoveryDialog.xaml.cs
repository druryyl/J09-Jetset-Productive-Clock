using System.Windows;
using Jetset.App.Models;
using Jetset.App.ViewModels;

namespace Jetset.App.Views;

public enum RecoveryResult
{
    Continue,
    FinishLastKnown,
    Discard
}

public partial class RecoveryDialog : Window
{
    public RecoveryDialog(WorkSession session)
    {
        InitializeComponent();
        DataContext = new RecoveryViewModel(session);
        Result = RecoveryResult.Continue;
    }

    public RecoveryResult Result { get; private set; }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        Result = RecoveryResult.Continue;
        DialogResult = true;
    }

    private void Finish_Click(object sender, RoutedEventArgs e)
    {
        Result = RecoveryResult.FinishLastKnown;
        DialogResult = true;
    }

    private void Discard_Click(object sender, RoutedEventArgs e)
    {
        Result = RecoveryResult.Discard;
        DialogResult = true;
    }
}
