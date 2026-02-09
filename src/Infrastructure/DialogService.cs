using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace StreamTalkerClient.Infrastructure;

/// <summary>
/// Shared dialog helper methods for ViewModels.
/// Provides resource lookup, window resolution, and simple message/confirm dialogs.
/// </summary>
public static class DialogService
{
    public static string GetResource(string key)
    {
        if (Application.Current?.Resources.TryGetResource(key, null, out var value) == true
            && value is string str)
        {
            return str;
        }
        return key;
    }

    public static Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            foreach (var window in desktop.Windows)
            {
                if (window is { IsActive: true })
                    return window;
            }
            return desktop.MainWindow;
        }
        return null;
    }

    public static async Task ShowMessageAsync(string title, string message)
    {
        var owner = GetMainWindow();
        if (owner == null) return;

        var dialog = new Window
        {
            Title = title,
            Width = 320,
            Height = 140,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false
        };

        var okButton = new Button
        {
            Content = GetResource("OkButton"),
            Width = 80,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 10, 0, 10)
        };
        okButton.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(15),
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Margin = new Avalonia.Thickness(0, 0, 0, 10)
                },
                okButton
            }
        };

        await dialog.ShowDialog(owner);
    }

    public static async Task<bool> ShowConfirmAsync(string title, string message)
    {
        var owner = GetMainWindow();
        if (owner == null) return false;

        var confirmed = false;

        var dialog = new Window
        {
            Title = title,
            Width = 360,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false
        };

        var yesButton = new Button
        {
            Content = GetResource("YesButton"),
            Width = 80,
            Margin = new Avalonia.Thickness(0, 0, 5, 0)
        };
        yesButton.Click += (_, _) =>
        {
            confirmed = true;
            dialog.Close();
        };

        var noButton = new Button
        {
            Content = GetResource("NoButton"),
            Width = 80
        };
        noButton.Click += (_, _) => dialog.Close();

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Spacing = 5,
            Margin = new Avalonia.Thickness(0, 10, 0, 0),
            Children = { yesButton, noButton }
        };

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(15),
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Margin = new Avalonia.Thickness(0, 0, 0, 10)
                },
                buttonPanel
            }
        };

        await dialog.ShowDialog(owner);
        return confirmed;
    }
}
