using System;
using System.Globalization;
using System.Windows.Data;

namespace HttpFileServer.Converters
{
    // Subtracts a fixed value (provided via ConverterParameter) from the input double.
    // Used to compute a MaxWidth for the left column so the GridSplitter cannot be dragged
    // entirely out of view (keeps right column at its MinWidth).
    public class SubtractConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is double total))
                return Binding.DoNothing;

            double subtract = 0;
            if (parameter != null)
            {
                double.TryParse(parameter.ToString(), out subtract);
            }

            var result = total - subtract;
            if (result < 0) result = 0;
            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}