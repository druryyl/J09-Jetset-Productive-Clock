using System.Windows;
using Jetset.App.Services;
using Jetset.App.ViewModels;

namespace Jetset.App.Views;

public partial class HistoryWindow : Window
{
    public HistoryWindow(AppServices services)
    {
        InitializeComponent();
        DataContext = new HistoryViewModel(services);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
