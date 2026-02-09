using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace StreamTalkerClient.Views.Wizard;

public partial class CompletedView : UserControl
{
    public CompletedView()
    {
        InitializeComponent();
    }

    private void OnStartButtonClick(object? sender, RoutedEventArgs e)
    {
        // Close the wizard window
        var window = this.FindAncestorOfType<Window>();
        window?.Close();
    }
}
