using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using StreamTalkerClient.Models;

namespace StreamTalkerClient.Converters;

public class NotificationSeverityToBrushConverter : IValueConverter
{
    public string Mode { get; set; } = "Background";

    private static readonly IBrush InfoAccent = new SolidColorBrush(Color.Parse("#3B82F6"));
    private static readonly IBrush SuccessAccent = new SolidColorBrush(Color.Parse("#22C55E"));
    private static readonly IBrush WarningAccent = new SolidColorBrush(Color.Parse("#F59E0B"));
    private static readonly IBrush DangerAccent = new SolidColorBrush(Color.Parse("#EF4444"));

    private static readonly IBrush InfoBg = new SolidColorBrush(Color.Parse("#203B82F6"));
    private static readonly IBrush SuccessBg = new SolidColorBrush(Color.Parse("#2022C55E"));
    private static readonly IBrush WarningBg = new SolidColorBrush(Color.Parse("#20F59E0B"));
    private static readonly IBrush DangerBg = new SolidColorBrush(Color.Parse("#20EF4444"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var severity = value is NotificationSeverity s ? s : NotificationSeverity.Info;
        var mode = parameter as string ?? Mode;

        if (mode == "Accent")
        {
            return severity switch
            {
                NotificationSeverity.Success => SuccessAccent,
                NotificationSeverity.Warning => WarningAccent,
                NotificationSeverity.Danger => DangerAccent,
                _ => InfoAccent
            };
        }

        return severity switch
        {
            NotificationSeverity.Success => SuccessBg,
            NotificationSeverity.Warning => WarningBg,
            NotificationSeverity.Danger => DangerBg,
            _ => InfoBg
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
