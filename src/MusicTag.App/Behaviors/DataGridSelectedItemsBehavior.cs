using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;

namespace MusicTag.App.Behaviors;

/// <summary>
/// <see cref="DataGrid.SelectedItems"/> is a read-only <see cref="IList"/> proxy onto the grid's
/// live selection, not a bindable <see cref="AvaloniaProperty"/> — same limitation as WPF's
/// DataGrid, and the same fix: this attached behavior (via Avalonia.Xaml.Interactivity) listens
/// to the grid's native <see cref="DataGrid.SelectionChanged"/> event and mirrors additions/
/// removals into a bound <see cref="IList"/> — in practice
/// <c>ObservableCollection&lt;MusicTag.App.ViewModels.FileListItemViewModel&gt;</c> on
/// <c>MainWindowViewModel</c> — one item at a time, so the view model always reflects exactly
/// what's currently selected in the grid.
///
/// Deliberately one-directional (grid -> view model): nothing in this app programmatically
/// drives the grid's selection from the view model. Folder-open/Refresh clears
/// <c>MainWindowViewModel.Files</c> (the grid's ItemsSource), which the grid itself turns into a
/// <see cref="DataGrid.SelectionChanged"/> with everything in
/// <see cref="SelectionChangedEventArgs.RemovedItems"/> — so the bound collection empties out
/// through this same path rather than needing an explicit reverse sync.
/// </summary>
public sealed class DataGridSelectedItemsBehavior : Behavior<DataGrid>
{
    public static readonly StyledProperty<IList?> SelectedItemsProperty =
        AvaloniaProperty.Register<DataGridSelectedItemsBehavior, IList?>(nameof(SelectedItems));

    public IList? SelectedItems
    {
        get => GetValue(SelectedItemsProperty);
        set => SetValue(SelectedItemsProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject is not null)
        {
            AssociatedObject.SelectionChanged += OnSelectionChanged;
        }
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject is not null)
        {
            AssociatedObject.SelectionChanged -= OnSelectionChanged;
        }

        base.OnDetaching();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var target = SelectedItems;
        if (target is null)
            return;

        foreach (var removed in e.RemovedItems)
            target.Remove(removed);

        foreach (var added in e.AddedItems)
        {
            if (!target.Contains(added))
                target.Add(added);
        }
    }
}
