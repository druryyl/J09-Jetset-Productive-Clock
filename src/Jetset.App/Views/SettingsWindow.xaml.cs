using System.Windows;
using Jetset.App.Services;
using Jetset.App.ViewModels;

namespace Jetset.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(AppServices services)
    {
        InitializeComponent();
        DataContext = new SettingsViewModel(services.Settings);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
