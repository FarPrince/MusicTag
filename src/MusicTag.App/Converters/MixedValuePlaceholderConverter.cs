using System.Globalization;
using Avalonia.Data.Converters;
using MusicTag.App.ViewModels;

namespace MusicTag.App.Converters;

/// <summary>
/// Converts an <see cref="EditPanelViewModel"/> field's <see cref="MixedValue{T}"/> snapshot
/// into the "&lt;keep&gt;" hint text shown via <c>Watermark</c> (Avalonia TextBox's placeholder-
/// text property; WPF-UI's TextBox called the same concept PlaceholderText) — the real value
/// when all agree is handled by the plain Text binding, so this converter only ever needs to
/// answer "is this field currently mixed," which is why it targets the non-generic
/// <see cref="IMixedValue"/> marker rather than a specific closed <c>MixedValue&lt;T&gt;</c>,
/// letting one converter instance serve every field regardless of value type.
/// </summary>
public sealed class MixedValuePlaceholderConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is IMixedValue { IsMixed: true } ? "<keep>" : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
