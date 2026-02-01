using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;

namespace TodoWpfApp
{
    public class StatusTagConverter : IValueConverter
    {
        private static readonly string[] StatusTags = { "New", "In Progress", "On Hold", "Complete" };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable<string> tags)
            {
                var match = tags.FirstOrDefault(t => StatusTags.Any(s => string.Equals(s, t, StringComparison.OrdinalIgnoreCase)));
                if (!string.IsNullOrWhiteSpace(match))
                {
                    return match;
                }
            }

            return "New";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = value as string ?? string.Empty;
            return StatusToBrush(status);
        }

        protected static Brush StatusToBrush(string status)
        {
            return status.Trim().ToLowerInvariant() switch
            {
                "new" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95a5a6")),
                "in progress" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f39c12")),
                "on hold" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8e44ad")),
                "complete" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27ae60")),
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95a5a6"))
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusToForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusTagsToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable<string> tags)
            {
                var status = tags.FirstOrDefault(t => StatusTagConverterMatches(t, StatusTagConverterTags));
                return StatusToBrush(status ?? "New");
            }

            return StatusToBrush("New");
        }

        private static readonly string[] StatusTagConverterTags = { "New", "In Progress", "On Hold", "Complete" };

        private static bool StatusTagConverterMatches(string tag, IEnumerable<string> tags)
        {
            return tags.Any(s => string.Equals(s, tag, StringComparison.OrdinalIgnoreCase));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static Brush StatusToBrush(string status)
        {
            return status.Trim().ToLowerInvariant() switch
            {
                "new" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95a5a6")),
                "in progress" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f39c12")),
                "on hold" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8e44ad")),
                "complete" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27ae60")),
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95a5a6"))
            };
        }
    }

    public class StatusTagsToForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
