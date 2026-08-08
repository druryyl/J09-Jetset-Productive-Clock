using System.Windows;

namespace Jetset.App.Views;

public partial class FinishNoteDialog : Window
{
    public FinishNoteDialog()
    {
        InitializeComponent();
    }

    public string? Note { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Note = NoteBox.Text;
        DialogResult = true;
    }
}
