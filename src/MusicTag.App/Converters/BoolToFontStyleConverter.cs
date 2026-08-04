using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace MusicTag.App.Converters;

/// <summary>
/// Drives the file grid's dirty-row indicator — the whole row renders in italic font while its
/// underlying <see cref="MusicTag.Core.Models.AudioFile.IsDirty"/> is true. Bound directly from
/// a <c>DataGridRow</c>-scoped Style in MainWindow.axaml (Avalonia Styles support a
/// <c>{Binding}</c> inside a Setter's Value the same way WPF's DataTrigger did), rather than the
/// custom-ControlTemplate-plus-DataTrigger approach the WPF original used — see MainWindow.axaml
/// .cs's own doc comment on why this port doesn't replace the DataGridRow template wholesale.
/// </summary>
public sealed class BoolToFontStyleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? FontStyle.Italic : FontStyle.Normal;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
