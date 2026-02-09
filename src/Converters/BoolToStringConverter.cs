using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace StreamTalkerClient.Converters;

/// <summary>
/// Converts a bool to one of two localized strings.
/// ConverterParameter format: "TrueKey|FalseKey" where keys are DynamicResource keys.
/// </summary>
public class BoolToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string param)
            return "";

        var parts = param.Split('|');
        if (parts.Length != 2)
            return "";

        var key = value is true ? parts[0] : parts[1];

        if (Application.Current != null &&
            Application.Current.TryGetResource(key, Application.Current.ActualThemeVariant, out var resource) &&
            resource is string str)
        {
            return str;
        }

        return key;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
