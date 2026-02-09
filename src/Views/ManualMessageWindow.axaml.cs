using Avalonia.Controls;
using StreamTalkerClient.ViewModels;

namespace StreamTalkerClient.Views;

public partial class ManualMessageWindow : Window
{
    public ManualMessageWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is ManualMessageViewModel viewModel)
        {
            // Subscribe to close request
            viewModel.CloseRequested += () => Close();
        }
    }
}
