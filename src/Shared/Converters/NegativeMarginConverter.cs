using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Overlayer.Shared.Converters;

public class NegativeMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int margin)
            return new Thickness(-margin);
        if (value is double marginD)
            return new Thickness(-marginD);
        return new Thickness(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
