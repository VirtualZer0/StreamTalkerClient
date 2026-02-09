using Avalonia;
using Avalonia.Controls;
using StreamTalkerClient.ViewModels.Wizard;

namespace StreamTalkerClient.Views.Wizard;

public partial class ServerLaunchView : UserControl
{
    public ServerLaunchView()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Trigger server launch when this view becomes visible (i.e. user navigated to this step),
        // not when DataContext is assigned (which happens at window creation for all views).
        if (change.Property == IsVisibleProperty && IsVisible
            && DataContext is FirstLaunchWizardViewModel vm && !vm.IsLoading)
        {
            _ = vm.LaunchServerCommand.ExecuteAsync(null);
        }
    }
}
