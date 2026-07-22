using System.Globalization;
using System.Windows.Data;
using Binding = System.Windows.Data.Binding;

namespace MusicTag.App.Converters;

/// <summary>
/// Two-way conversion between an editable <c>int?</c> tag field (Year/Track#/Disc#) and the
/// plain text a <c>ui:TextBox</c> actually holds. Empty/whitespace text converts back to
/// null (clears the field); unparsable text is rejected via <see cref="Binding.DoNothing"/>
/// so a bad keystroke doesn't silently null out the field.
/// </summary>
public sealed class NullableIntToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int i ? i.ToString(culture) : string.Empty;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text))
            return null!;

        return int.TryParse(text, NumberStyles.Integer, culture, out var result)
            ? result
            : Binding.DoNothing;
    }
}
