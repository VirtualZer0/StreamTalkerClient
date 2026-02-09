using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Serilog;
using StreamTalkerClient.Infrastructure;
using StreamTalkerClient.Models;
using StreamTalkerClient.ViewModels;
using StreamTalkerClient.Views;

namespace StreamTalkerClient;

public partial class App : Application
{
    private MainWindowViewModel? _viewModel;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Load saved language preference
        var settings = SettingsRepository.Load();
        if (settings.Metadata.LanguageUI != "en")
        {
            SetLanguage(settings.Metadata.LanguageUI);
        }
    }

    /// <summary>
    /// Detects the system language and returns the appropriate language code ("en" or "ru").
    /// Falls back to "en" for unsupported languages.
    /// </summary>
    private static string DetectSystemLanguage()
    {
        try
        {
            var culture = CultureInfo.CurrentUICulture;
            var langCode = culture.TwoLetterISOLanguageName.ToLowerInvariant();

            // Return "ru" for Russian, otherwise fallback to "en"
            return langCode == "ru" ? "ru" : "en";
        }
        catch
        {
            return "en"; // Fallback to English on any error
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = SettingsRepository.Load();

            if (!settings.Metadata.HasCompletedWizard)
            {
                // First launch - show wizard
                var wizardWindow = new Views.Wizard.FirstLaunchWizardWindow();
                desktop.MainWindow = wizardWindow;
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

                wizardWindow.Closed += (s, e) =>
                {
                    // Reload settings to check if wizard completed
                    var updatedSettings = SettingsRepository.Load();
                    
                    if (updatedSettings.Metadata.HasCompletedWizard)
                    {
                        // Wizard completed successfully - create main window
                        _viewModel = new MainWindowViewModel();
                        var mainWindow = new MainWindow { DataContext = _viewModel };
                        desktop.MainWindow = mainWindow;
                        SetupTrayIcon(mainWindow);
                        mainWindow.Show();
                    }
                    else
                    {
                        // User cancelled wizard - exit app
                        desktop.Shutdown();
                    }
                };
            }
            else
            {
                // Normal launch - wizard already completed
                _viewModel = new MainWindowViewModel();
                var mainWindow = new MainWindow { DataContext = _viewModel };
                desktop.MainWindow = mainWindow;
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                SetupTrayIcon(mainWindow);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(MainWindow mainWindow)
    {
        var showText = GetResourceString("ShowButton", "Show");
        var exitText = GetResourceString("ExitButton", "Exit");

        var trayIcon = new TrayIcon
        {
            ToolTipText = "Stream Talker Client",
            Icon = CreateTrayIconImage(),
            IsVisible = true
        };

        var showItem = new NativeMenuItem(showText);
        showItem.Click += (_, _) => mainWindow.RestoreFromTray();

        var exitItem = new NativeMenuItem(exitText);
        exitItem.Click += (_, _) => mainWindow.Close();

        trayIcon.Menu = new NativeMenu();
        trayIcon.Menu.Items.Add(showItem);
        trayIcon.Menu.Items.Add(exitItem);

        trayIcon.Clicked += (_, _) => mainWindow.RestoreFromTray();

        var trayIcons = new TrayIcons { trayIcon };
        SetValue(TrayIcon.IconsProperty, trayIcons);
    }

    private static WindowIcon CreateTrayIconImage()
    {
        try
        {
            // Try to load the brand icon from assets
            var assetLoader = AssetLoader.Open(new Uri("avares://StreamTalkerClient/Assets/Icons/icon-32.png"));
            return new WindowIcon(assetLoader);
        }
        catch (Exception ex)
        {
            // Log the error and fall back to programmatically generated icon
            Log.Warning(ex, "Failed to load tray icon from assets, falling back to generated icon");

            // Fallback: create green circle as before
            int size = 32;

            var circle = new Ellipse
            {
                Width = size - 4,
                Height = size - 4,
                Fill = new SolidColorBrush(Color.Parse("#4CAF50")),
                Margin = new Thickness(2)
            };

            circle.Measure(new Size(size, size));
            circle.Arrange(new Rect(0, 0, size, size));

            var rtb = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
            rtb.Render(circle);

            using var ms = new MemoryStream();
            rtb.Save(ms);
            ms.Position = 0;
            return new WindowIcon(ms);
        }
    }

    private static string GetResourceString(string key, string fallback)
    {
        if (Current != null &&
            Current.TryGetResource(key, Current.ActualThemeVariant, out var resource) &&
            resource is string str)
        {
            return str;
        }
        return fallback;
    }

    public static void SetLanguage(string langCode)
    {
        var app = Current;
        if (app == null) return;

        var dictionaries = app.Resources.MergedDictionaries;

        // Remove existing language dictionary
        var existing = dictionaries
            .OfType<ResourceInclude>()
            .FirstOrDefault(d => d.Source?.ToString().Contains("/Lang/") == true);

        if (existing != null)
            dictionaries.Remove(existing);

        // Add new language
        dictionaries.Add(new ResourceInclude(new Uri("avares://StreamTalkerClient/"))
        {
            Source = new Uri($"/Lang/{langCode}.axaml", UriKind.Relative)
        });
    }
}
