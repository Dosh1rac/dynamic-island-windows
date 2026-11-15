using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using DynamicIsland.Models;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace DynamicIsland.Helpers
{
    public class EqualizerHeightConverter : IValueConverter
    {
        public double MaxHeight { get; set; } = 20.0;
        public double MinHeight { get; set; } = 3.0;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is float f)
            {
                double height = MinHeight + (MaxHeight - MinHeight) * Math.Clamp(f, 0f, 1f);
                return height;
            }
            return MinHeight;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ModeActiveBrushConverter : IValueConverter
    {
        public Brush ActiveBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0, 122, 255));
        public Brush InactiveBrush { get; set; } = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is DisplayMode mode && parameter is string paramStr)
            {
                if (Enum.TryParse<DisplayMode>(paramStr, true, out var targetMode) && mode == targetMode)
                {
                    return ActiveBrush;
                }
            }
            return InactiveBrush;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isNull = value == null;
            if (Invert) isNull = !isNull;
            return isNull ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BooleanToVisibilityInvertedConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class CornerRadiusConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                return new CornerRadius(d);
            }
            return new CornerRadius(18);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
