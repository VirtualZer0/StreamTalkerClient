using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using StreamTalkerClient.ViewModels;

namespace StreamTalkerClient.Views;

public partial class MainWindow : Window
{
    private SettingsWindow? _settingsWindow;
    private double _baseWidth;
    private bool _isUpdateDialogOpen;
    private StreamTalkerClient.Models.ServerUpdateInfo? _pendingServerUpdate;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnWindowClosing;
        _baseWidth = Width;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainWindowViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            vm.ShowUpdateRequested += OnShowUpdateRequested;
            vm.ShowServerUpdateRequested += OnShowServerUpdateRequested;

            // Apply initial queue panel state
            if (vm.IsQueuePanelOpen)
            {
                AdjustWindowWidth(true);
            }
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.IsSettingsWindowOpen):
                if (DataContext is MainWindowViewModel vm1 && vm1.IsSettingsWindowOpen)
                {
                    OpenSettingsWindow();
                }
                break;

            case nameof(MainWindowViewModel.IsQueuePanelOpen):
                if (DataContext is MainWindowViewModel vm2)
                {
                    AdjustWindowWidth(vm2.IsQueuePanelOpen);
                }
                break;
        }
    }

    private void OpenSettingsWindow()
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow
        {
            DataContext = DataContext
        };
        _settingsWindow.Closed += (_, _) =>
        {
            _settingsWindow = null;
        };
        _settingsWindow.Show(this);
    }

    private void AdjustWindowWidth(bool queueOpen)
    {
        const int queuePanelWidth = 310; // 280 content + 28 toggle + border

        // Make queue column responsive: Star when open, Auto when closed
        if (RootGrid.ColumnDefinitions.Count > 2)
        {
            var queueCol = RootGrid.ColumnDefinitions[2];
            queueCol.Width = queueOpen ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
            queueCol.MinWidth = queueOpen ? 280 : 0;
        }

        if (queueOpen)
        {
            Width = _baseWidth + queuePanelWidth;
        }
        else
        {
            _baseWidth = Width - queuePanelWidth;
            if (_baseWidth < MinWidth)
                _baseWidth = MinWidth;
            Width = _baseWidth;
        }

        // Clamp to screen bounds
        var screen = Screens.ScreenFromWindow(this);
        if (screen != null)
        {
            var maxWidth = screen.WorkingArea.Width / screen.Scaling;
            if (Width > maxWidth)
                Width = maxWidth;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty &&
            change.NewValue is WindowState state &&
            state == WindowState.Minimized)
        {
            Hide();
            ShowInTaskbar = false;
        }
    }

    public void RestoreFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private async void OnManageVoicesClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;

        var vm = new VoiceManagementViewModel(viewModel.TtsClient, viewModel.Settings);
        var dialog = new VoiceManagementWindow(vm);
        await dialog.ShowDialog<bool?>(this);

        // Refresh voices after dialog closes
        if (viewModel.RefreshVoicesCommand.CanExecute(null))
            await viewModel.RefreshVoicesCommand.ExecuteAsync(null);
    }

    private async void OnShowUpdateRequested(StreamTalkerClient.Models.UpdateInfo updateInfo)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;
        if (_isUpdateDialogOpen) return;

        _isUpdateDialogOpen = true;
        try
        {
            var vm = new UpdateDialogViewModel(viewModel.UpdateService, updateInfo);
            vm.SkipVersionRequested += version =>
            {
                viewModel.Settings.Metadata.SkippedClientVersion = version;
                viewModel.Settings.Save();
            };
            var dialog = new UpdateDialogWindow(vm);
            await dialog.ShowDialog(this);
        }
        finally
        {
            _isUpdateDialogOpen = false;
        }

        // Show pending server update dialog if queued while client dialog was open
        if (_pendingServerUpdate != null)
        {
            var pending = _pendingServerUpdate;
            _pendingServerUpdate = null;
            OnShowServerUpdateRequested(pending);
        }
    }

    private async void OnShowServerUpdateRequested(StreamTalkerClient.Models.ServerUpdateInfo serverUpdateInfo)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;

        // Queue if another update dialog is already open
        if (_isUpdateDialogOpen)
        {
            _pendingServerUpdate = serverUpdateInfo;
            return;
        }

        _isUpdateDialogOpen = true;
        try
        {
            var vm = new ServerUpdateDialogViewModel(serverUpdateInfo);
            vm.SkipVersionRequested += version =>
            {
                viewModel.Settings.Metadata.SkippedServerVersion = version;
                viewModel.Settings.Save();
            };
            var dialog = new ServerUpdateDialogWindow(vm);
            await dialog.ShowDialog(this);
        }
        finally
        {
            _isUpdateDialogOpen = false;
        }
    }

    private async void OnAddManualMessageClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;

        var vm = new ManualMessageViewModel(
            viewModel.Settings,
            viewModel.QueueManager,
            viewModel.AvailableVoices);

        var dialog = new ManualMessageWindow { DataContext = vm };
        await dialog.ShowDialog(this);
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        _settingsWindow?.Close();

        if (DataContext is MainWindowViewModel vm)
        {
            vm.PropertyChanged -= OnViewModelPropertyChanged;
            vm.ShowUpdateRequested -= OnShowUpdateRequested;
            vm.ShowServerUpdateRequested -= OnShowServerUpdateRequested;
            vm.Cleanup();
            vm.Dispose();
        }
    }
}
