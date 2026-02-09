using Avalonia.Controls;
using StreamTalkerClient.ViewModels;

namespace StreamTalkerClient.Views;

public partial class VoiceBindingWindow : Window
{
    public VoiceBindingWindow()
    {
        InitializeComponent();
    }

    public VoiceBindingWindow(VoiceBindingViewModel viewModel) : this()
    {
        DataContext = viewModel;

        viewModel.CloseRequested += () =>
        {
            Close(viewModel.BindingsModified);
        };

        // Load data after the window is opened
        Opened += async (_, _) =>
        {
            await viewModel.InitializeAsync();
        };
    }
}
