using Avalonia.Controls;
using Avalonia.Interactivity;
using StreamTalkerClient.ViewModels;

namespace StreamTalkerClient.Views;

public partial class VoiceSettingsPanel : UserControl
{
    public VoiceSettingsPanel()
    {
        InitializeComponent();
    }

    private async void OnVoiceBindingsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;
        if (TopLevel.GetTopLevel(this) is not Window parentWindow) return;

        var vm = new VoiceBindingViewModel(viewModel.TtsClient, viewModel.Settings);
        var dialog = new VoiceBindingWindow(vm);
        await dialog.ShowDialog<bool?>(parentWindow);
    }
}
