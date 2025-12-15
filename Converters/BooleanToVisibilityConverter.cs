using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MMG.Converters
{
    /// <summary>
    /// Boolean 값을 Visibility로 변환
    /// ConverterParameter에 "Inverse"를 전달하면 반대 동작
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = value is bool b && b;
            bool inverse = parameter?.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) == true;

            if (inverse)
                boolValue = !boolValue;

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool visibleValue = value is Visibility v && v == Visibility.Visible;
            bool inverse = parameter?.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) == true;

            return inverse ? !visibleValue : visibleValue;
        }
    }
}
