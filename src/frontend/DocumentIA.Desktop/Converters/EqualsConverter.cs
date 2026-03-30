using System;
using System.Globalization;
using System.Windows.Data;

namespace DocumentIA.Desktop.Converters
{
    public class EqualsConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            var left = value.ToString();
            var right = parameter.ToString();
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue && parameter != null)
                return parameter.ToString();

            return null;
        }
    }
}
