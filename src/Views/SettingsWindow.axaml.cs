using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using StreamTalkerClient.Infrastructure;
using StreamTalkerClient.Models;
using StreamTalkerClient.ViewModels;

namespace StreamTalkerClient.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Closing += OnWindowClosing;
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.IsSettingsWindowOpen = false;
        }
    }

    private static string L(string key, string fallback = "")
    {
        if (Application.Current != null &&
            Application.Current.TryFindResource(key, out var value) &&
            value is string s)
            return s;
        return fallback;
    }

    private async void OnExportSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = L("ExportSettingsButton", "Export Settings"),
            DefaultExtension = "json",
            SuggestedFileName = "streamtalker-settings.json",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("JSON") { Patterns = new[] { "*.json" } }
            }
        });

        if (file == null) return;

        try
        {
            // Save current state first
            vm.Settings.Save();

            var json = JsonSerializer.Serialize(vm.Settings, AppJsonSerializerContext.Default.AppSettings);
            await using var stream = await file.OpenWriteAsync();
            stream.SetLength(0);
            await using var writer = new System.IO.StreamWriter(stream);
            await writer.WriteAsync(json);
        }
        catch (Exception ex)
        {
            vm.ShowNotification(
                string.Format(L("ExportFailedNotification", "Export failed: {0}"), ex.Message),
                NotificationSeverity.Danger);
        }
    }

    private async void OnImportSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = L("ImportSettingsButton", "Import Settings"),
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("JSON") { Patterns = new[] { "*.json" } }
            }
        });

        if (files.Count == 0) return;

        try
        {
            await using var stream = await files[0].OpenReadAsync();
            using var reader = new System.IO.StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            var imported = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.AppSettings);

            if (imported == null)
            {
                vm.ShowNotification(
                    L("InvalidSettingsFile", "Invalid settings file"),
                    NotificationSeverity.Danger);
                return;
            }

            // Copy imported values to current settings and reload
            vm.ImportSettings(imported);
            vm.ShowNotification(
                L("ImportSuccessNotification", "Settings imported successfully"),
                NotificationSeverity.Success);
        }
        catch (Exception ex)
        {
            vm.ShowNotification(
                string.Format(L("ImportFailedNotification", "Import failed: {0}"), ex.Message),
                NotificationSeverity.Danger);
        }
    }
}
