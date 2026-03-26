using DocumentIA.Desktop.Models;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DocumentIA.Desktop.Converters
{
    public class ActivityStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ActivityStatusEnum status)
            {
                return status switch
                {
                    ActivityStatusEnum.Completed => new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Green
                    ActivityStatusEnum.Running => new SolidColorBrush(Color.FromRgb(255, 193, 7)), // Amber
                    ActivityStatusEnum.Failed => new SolidColorBrush(Color.FromRgb(244, 67, 54)), // Red
                    _ => new SolidColorBrush(Color.FromRgb(189, 189, 189)) // Gray
                };
            }
            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ActivityStatusToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ActivityStatusEnum status)
            {
                return status switch
                {
                    ActivityStatusEnum.Completed => "✓",
                    ActivityStatusEnum.Running => "▶",
                    ActivityStatusEnum.Failed => "✗",
                    _ => "◯"
                };
            }
            return "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (System.Windows.Visibility)value == System.Windows.Visibility.Visible;
        }
    }

    public class DurationToDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long duration)
            {
                return $"{duration}ms";
            }
            if (value == null)
            {
                return "";
            }
            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
