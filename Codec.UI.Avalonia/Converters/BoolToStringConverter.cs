namespace Codec.UI.Avalonia.Converters
{
    using System;
    using System.Globalization;
    using global::Avalonia.Data.Converters;

    public class BoolToStringConverter : IValueConverter
    {
        public string TrueValue { get; set; } = "True";

        public string FalseValue { get; set; } = "False";

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is true ? TrueValue : FalseValue;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
