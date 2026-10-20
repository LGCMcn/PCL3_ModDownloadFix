using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MeloongCore.Wpf;

/// <summary>
/// 对数据绑定进行加法运算，使用 <paramref name="parameter"/> 决定加数。
/// </summary>
public class AdditionConverter : IValueConverter {
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is null) return 0;
        if (!double.TryParse(value.ToString(), out double before)) return 0;
        double scale = 1;
        if (parameter is not null) double.TryParse(parameter.ToString(), out scale);
        return before + scale;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is null) return Binding.DoNothing;
        if (!double.TryParse(value.ToString(), out double before)) return Binding.DoNothing;
        double scale = 1;
        if (parameter is not null) double.TryParse(parameter.ToString(), out scale);
        return before - scale;
    }
}

/// <summary>
/// 对数据绑定进行乘法运算，使用 <paramref name="parameter"/> 决定乘数。
/// </summary>
public class MultiplicationConverter : IValueConverter {
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is null) return 0;
        if (!double.TryParse(value.ToString(), out double before)) return 0;
        double scale = 1;
        if (parameter is not null) double.TryParse(parameter.ToString(), out scale);
        return before * scale;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is null) return Binding.DoNothing;
        if (!double.TryParse(value.ToString(), out double before)) return Binding.DoNothing;
        double scale = 1;
        if (parameter is not null) double.TryParse(parameter.ToString(), out scale);
        if (scale == 0) return Binding.DoNothing;
        return before / scale;
    }
}

/// <summary>
/// 将取反的 <see cref="bool"/> 绑定到 <see cref="Visibility"/>。
/// </summary>
public class InverseBooleanToVisibilityConverter : IValueConverter {
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is null) return Visibility.Visible;
        return bool.TryParse(value.ToString(), out bool boolValue) ? (boolValue ? Visibility.Collapsed : Visibility.Visible) : Visibility.Visible;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is null) return false;
        return value is Visibility vis && vis != Visibility.Visible;
    }
}

/// <summary>
/// 将 <see cref="bool"/> 取反。
/// </summary>
public class InverseBooleanConverter : IValueConverter {
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is null) return false;
        return bool.TryParse(value.ToString(), out bool boolValue) ? !boolValue : false;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is null) return false;
        return bool.TryParse(value.ToString(), out bool boolValue) ? !boolValue : false;
    }
}