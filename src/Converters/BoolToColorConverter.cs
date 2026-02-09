using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace StreamTalkerClient.Converters;

public class BoolToColorConverter : IValueConverter
{
    public IBrush TrueBrush { get; set; } = Brushes.LimeGreen;
    public IBrush FalseBrush { get; set; } = Brushes.Gray;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? TrueBrush : FalseBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
