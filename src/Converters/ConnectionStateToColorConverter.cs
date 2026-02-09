using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using StreamTalkerClient.Models;

namespace StreamTalkerClient.Converters;

public class ConnectionStateToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is StreamConnectionState state)
        {
            return state switch
            {
                StreamConnectionState.Disconnected => Brushes.Gray,
                StreamConnectionState.Connecting => Brushes.Orange,
                StreamConnectionState.Connected => Brushes.DodgerBlue,
                StreamConnectionState.Joined => Brushes.LimeGreen,
                StreamConnectionState.Error => Brushes.Red,
                _ => Brushes.Gray
            };
        }

        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
