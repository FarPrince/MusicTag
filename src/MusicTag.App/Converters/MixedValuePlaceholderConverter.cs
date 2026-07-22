using System.Globalization;
using System.Windows.Data;
using MusicTag.App.ViewModels;

namespace MusicTag.App.Converters;

/// <summary>
/// Converts an <see cref="EditPanelViewModel"/> field's <see cref="MixedValue{T}"/> snapshot
/// into the "&lt;keep&gt;" hint text shown via <c>ui:TextBox.PlaceholderText</c> — per plan
/// section 5: "a converter shows the real value when all agree, or a &lt;keep&gt; placeholder
/// (via PlaceholderText, NOT real Text) when mixed, so an untouched mixed field commits
/// nothing." The "real value when all agree" half of that sentence is handled by the plain
/// Text binding (EditPanelViewModel.Title etc. already holds the unanimous value in that
/// case) — this converter only ever needs to answer "is this field currently mixed," which is
/// why it targets the non-generic <see cref="IMixedValue"/> marker rather than a specific
/// closed <c>MixedValue&lt;T&gt;</c>, letting one converter instance serve every field
/// regardless of whether its value type is <c>string?</c> or <c>int?</c>.
/// </summary>
public sealed class MixedValuePlaceholderConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is IMixedValue { IsMixed: true } ? "<keep>" : string.Empty;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
