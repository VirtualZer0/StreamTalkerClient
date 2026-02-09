using Avalonia.Controls;
using StreamTalkerClient.ViewModels;

namespace StreamTalkerClient.Views;

public partial class VoiceManagementWindow : Window
{
    private VoiceManagementViewModel? _viewModel;
    private Action? _closeHandler;

    public VoiceManagementWindow()
    {
        InitializeComponent();
    }

    public VoiceManagementWindow(VoiceManagementViewModel viewModel) : this()
    {
        _viewModel = viewModel;
        DataContext = viewModel;

        _closeHandler = () => Close(viewModel.VoicesModified);
        viewModel.CloseRequested += _closeHandler;

        viewModel.EditVoiceRequested += OnEditVoiceRequested;
        viewModel.AddVoiceRequested += OnAddVoiceRequested;

        // Load data after the window is opened
        Opened += async (_, _) =>
        {
            await viewModel.InitializeAsync();
        };

        Closed += OnWindowClosed;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            if (_closeHandler != null)
            {
                _viewModel.CloseRequested -= _closeHandler;
                _closeHandler = null;
            }
            _viewModel.EditVoiceRequested -= OnEditVoiceRequested;
            _viewModel.AddVoiceRequested -= OnAddVoiceRequested;
        }
    }

    private async Task<bool> OnEditVoiceRequested(VoiceDisplayItem voice)
    {
        if (_viewModel == null) return false;

        var editVm = new VoiceEditViewModel(_viewModel.TtsClient, _viewModel.Settings, voice);
        var dialog = new VoiceEditWindow(editVm);
        var result = await dialog.ShowDialog<bool?>(this);
        return result == true;
    }

    private async Task<bool> OnAddVoiceRequested()
    {
        if (_viewModel == null) return false;

        var createVm = new VoiceEditViewModel(_viewModel.TtsClient, _viewModel.Settings);
        var dialog = new VoiceEditWindow(createVm);
        var result = await dialog.ShowDialog<bool?>(this);
        return result == true;
    }
}
