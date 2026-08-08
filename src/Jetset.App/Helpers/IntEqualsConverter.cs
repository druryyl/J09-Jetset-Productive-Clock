using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Jetset.App.Helpers;

public sealed class IntEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int current && parameter is not null
            && int.TryParse(parameter.ToString(), out var expected))
        {
            return current == expected;
        }

        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter is not null
            && int.TryParse(parameter.ToString(), out var expected))
        {
            return expected;
        }

        return System.Windows.Data.Binding.DoNothing;
    }
}
