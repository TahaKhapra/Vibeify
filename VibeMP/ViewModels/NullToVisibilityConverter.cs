using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VibeMP
{
    /// <summary>
    /// Prüft, ob ein Wert null ist. 
    /// Wenn ja, wird das Element versteckt (Hidden), damit das Layout unten stabil bleibt.
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Hidden : Visibility.Visible;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}