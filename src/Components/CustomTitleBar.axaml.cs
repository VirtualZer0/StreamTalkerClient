using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace StreamTalkerClient.Components;

public partial class CustomTitleBar : UserControl
{
    private Window? _parentWindow;
    private PathIcon? _maximizeRestoreIcon;
    private Button? _maximizeRestoreButton;

    public CustomTitleBar()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Get reference to parent window and controls
        _parentWindow = this.FindAncestorOfType<Window>();
        _maximizeRestoreIcon = this.FindControl<PathIcon>("MaximizeRestoreIcon");
        _maximizeRestoreButton = this.FindControl<Button>("MaximizeRestoreButton");

        if (_parentWindow != null)
        {
            // Subscribe to window property changes
            _parentWindow.PropertyChanged += OnWindowPropertyChanged;

            // Set initial state for all buttons
            UpdateButtonsVisibility();
            UpdateMaximizeRestoreIcon();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_parentWindow != null)
        {
            _parentWindow.PropertyChanged -= OnWindowPropertyChanged;
        }
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty)
        {
            UpdateMaximizeRestoreIcon();
        }
        else if (e.Property == Window.CanResizeProperty)
        {
            UpdateButtonsVisibility();
        }
    }

    private void UpdateButtonsVisibility()
    {
        if (_parentWindow == null) return;

        // Hide maximize/restore button if window cannot be resized
        if (_maximizeRestoreButton != null)
        {
            _maximizeRestoreButton.IsVisible = _parentWindow.CanResize;
        }
    }

    private void UpdateMaximizeRestoreIcon()
    {
        if (_parentWindow == null || _maximizeRestoreIcon == null) return;

        if (_parentWindow.WindowState == WindowState.Maximized)
        {
            _maximizeRestoreIcon.Data = Application.Current?.FindResource("WindowRestoreIcon") as StreamGeometry;
            if (_maximizeRestoreButton != null)
            {
                var restoreTooltip = Application.Current?.FindResource("WindowRestoreTooltip") as string ?? "Restore Down";
                ToolTip.SetTip(_maximizeRestoreButton, restoreTooltip);
            }
        }
        else
        {
            _maximizeRestoreIcon.Data = Application.Current?.FindResource("WindowMaximizeIcon") as StreamGeometry;
            if (_maximizeRestoreButton != null)
            {
                var maximizeTooltip = Application.Current?.FindResource("WindowMaximizeTooltip") as string ?? "Maximize";
                ToolTip.SetTip(_maximizeRestoreButton, maximizeTooltip);
            }
        }
    }

    private void OnDragRegionPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_parentWindow != null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _parentWindow.BeginMoveDrag(e);
        }
    }

    private void OnDragRegionDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (_parentWindow != null && _parentWindow.CanResize)
        {
            _parentWindow.WindowState = _parentWindow.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        if (_parentWindow != null)
        {
            _parentWindow.WindowState = WindowState.Minimized;
        }
    }

    private void OnMaximizeRestoreClick(object? sender, RoutedEventArgs e)
    {
        if (_parentWindow != null)
        {
            _parentWindow.WindowState = _parentWindow.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        _parentWindow?.Close();
    }
}
