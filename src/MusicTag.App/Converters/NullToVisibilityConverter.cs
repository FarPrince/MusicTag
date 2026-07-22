using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MusicTag.App.Converters;

/// <summary>
/// Collapses an element when the bound value is null (or an empty string), visible
/// otherwise. Used by the status bar's undo-stack description (plan section 5) so the
/// "Last change: ..." segment simply isn't shown at all while EditHistory's undo stack is
/// empty, rather than rendering an awkward "Last change: " with nothing after it.
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null or ""
            ? Visibility.Collapsed
            : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
