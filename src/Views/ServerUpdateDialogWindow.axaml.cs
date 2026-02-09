using Avalonia.Controls;
using StreamTalkerClient.ViewModels;

namespace StreamTalkerClient.Views;

public partial class ServerUpdateDialogWindow : Window
{
    private ServerUpdateDialogViewModel? _viewModel;
    private Action? _closeHandler;

    public ServerUpdateDialogWindow()
    {
        InitializeComponent();
    }

    public ServerUpdateDialogWindow(ServerUpdateDialogViewModel viewModel) : this()
    {
        _viewModel = viewModel;
        DataContext = viewModel;

        _closeHandler = () => Close();
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
}
