namespace Codec.UI.Avalonia.Converters
{
    using System;
    using System.Globalization;
    using global::Avalonia.Data.Converters;
    using Codec.UI.Avalonia.Models;

    public sealed class ViewModeConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not ViewMode current)
            {
                return false;
            }

            return parameter switch
            {
                ViewMode mode => current == mode,
                _ => false,
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Only act on the RadioButton becoming checked; ignore unchecks.
            if (value is not true)
            {
                return null;
            }

            return parameter switch
            {
                ViewMode mode => mode,
                _ => null,
            };
        }
    }
}
