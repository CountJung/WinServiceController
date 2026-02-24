using System.Globalization;
using System.Windows.Data;

namespace WinServiceController.Helpers
{
    [ValueConversion(typeof(bool), typeof(bool))]
    internal sealed class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool b ? !b : value;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool b ? !b : value;
    }
}
