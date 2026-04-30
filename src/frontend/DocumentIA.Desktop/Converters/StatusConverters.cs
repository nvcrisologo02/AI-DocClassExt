using DocumentIA.Desktop.Models;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DocumentIA.Desktop.Converters
{
    public class ActivityStatusToBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isConnected)
            {
                return isConnected 
                    ? new SolidColorBrush(Color.FromRgb(20, 184, 166))
                    : new SolidColorBrush(Color.FromRgb(239, 68, 68));
            }
            
            if (value is ActivityStatusEnum status)
            {
                return status switch
                {
                    ActivityStatusEnum.Completed => new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                    ActivityStatusEnum.Running => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                    ActivityStatusEnum.Skipped => new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                    ActivityStatusEnum.Failed => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                    _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))
                };
            }
            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ActivityStatusToStringConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ActivityStatusEnum status)
            {
                return status switch
                {
                    ActivityStatusEnum.Completed => "✓",
                    ActivityStatusEnum.Running => "▶",
                    ActivityStatusEnum.Skipped => "↷",
                    ActivityStatusEnum.Failed => "✗",
                    _ => "◯"
                };
            }
            return "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ActivityStatusToLabelConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ActivityStatusEnum status)
            {
                return status switch
                {
                    ActivityStatusEnum.Completed => "Completed",
                    ActivityStatusEnum.Running => "Running",
                    ActivityStatusEnum.Skipped => "Skipped",
                    ActivityStatusEnum.Failed => "Failed",
                    _ => "Pending"
                };
            }

            return "Pending";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is bool boolValue && boolValue ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is System.Windows.Visibility visibility && visibility == System.Windows.Visibility.Visible;
        }
    }

    public class DurationToDisplayConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is long duration)
            {
                return $"{duration}ms";
            }
            if (value == null)
            {
                return "";
            }
            return value.ToString() ?? string.Empty;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class HealthStatusToBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var status = value?.ToString()?.Trim().ToLowerInvariant();
            return status switch
            {
                "healthy" => new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                "degraded" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                "unhealthy" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                "unconfigured" => new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
