using System;
using System.Globalization;
using System.Windows.Data;

namespace HttpFileServer.Converters
{
    // Safely converts a string to a Uri for use with Hyperlink.NavigateUri.
    // Returns null for null, empty, or otherwise invalid URI strings so that
    // WPF does not throw the "Cannot convert ''" Error 23 at startup when the
    // bound text property has not yet been populated.
    public class StringToUriConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value as string;
            if (string.IsNullOrWhiteSpace(s))
                return null;

            return Uri.TryCreate(s, UriKind.Absolute, out var uri) ? uri : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value as Uri)?.ToString() ?? string.Empty;
        }
    }
}
