using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TodoWpfApp
{
    public class ReadToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool read && !read)
            {
                return new SolidColorBrush(Color.FromRgb(255, 255, 220)); // Light yellow for unread
            }

            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
