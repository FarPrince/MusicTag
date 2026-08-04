using System.Globalization;
using Avalonia.Data.Converters;

namespace MusicTag.App.Converters;

/// <summary>
/// Converts a bound value to false (hides the element) when it's null or an empty string, true
/// otherwise — bind to a control's <c>IsVisible</c> (Avalonia has no separate
/// Visible/Collapsed/Hidden enum the way WPF's Visibility does; every control is shown/hidden
/// via the one bool property). Used by the status bar's undo-stack description so the "Last
/// change: ..." segment simply isn't shown at all while EditHistory's undo stack is empty.
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not (null or "");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
