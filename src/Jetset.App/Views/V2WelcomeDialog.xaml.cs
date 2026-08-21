using System.Windows;

namespace Jetset.App.Views;

public partial class V2WelcomeDialog : Window
{
    public V2WelcomeDialog(bool upgradedFromV1)
    {
        InitializeComponent();
        DataContext = new ViewModels.V2WelcomeViewModel(upgradedFromV1);
    }

    private void GetStarted_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
