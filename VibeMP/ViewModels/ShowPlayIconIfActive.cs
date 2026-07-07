using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VibeMP
{
    /// <summary>
    /// Vergleicht das aktuell spielende Lied mit dem Lied in der Liste.
    /// Zeigt das Play-Symbol nur an, wenn es genau dasselbe Lied ist.
    /// </summary>
    public class ShowPlayIconIfActive : IMultiValueConverter
    {
        public object Convert(object?[]? values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values != null && values.Length == 2 && values[0] != null && values[1] != null)
            {
                return values[0].Equals(values[1]) ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}