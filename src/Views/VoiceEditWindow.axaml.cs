using Avalonia.Controls;
using Avalonia.Interactivity;
using StreamTalkerClient.ViewModels;

namespace StreamTalkerClient.Views;

public partial class VoiceEditWindow : Window
{
    private VoiceEditViewModel? _viewModel;
    private Action? _closeHandler;

    public VoiceEditWindow()
    {
        InitializeComponent();
    }

    public VoiceEditWindow(VoiceEditViewModel viewModel) : this()
    {
        _viewModel = viewModel;
        DataContext = viewModel;

        _closeHandler = () => Close(viewModel.WasModified);
        viewModel.CloseRequested += _closeHandler;

        Closed += OnWindowClosed;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (_viewModel != null && _closeHandler != null)
        {
            _viewModel.CloseRequested -= _closeHandler;
            _closeHandler = null;
        }
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            await _viewModel.BrowseFileAsync(this);
        }
    }
}
