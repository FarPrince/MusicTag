using System.Collections;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;

namespace MusicTag.App.Behaviors;

/// <summary>
/// <see cref="DataGrid.SelectedItems"/> is a read-only <see cref="IList"/> proxy onto the
/// grid's live selection, not a bindable <see cref="DependencyProperty"/> — there is no
/// built-in way to bind the grid's full multi-selection to a view model. This attached
/// behavior (via Microsoft.Xaml.Behaviors.Wpf, already referenced since M1 per plan section
/// 5's "Multi-select plumbing") bridges the gap: it listens to the grid's native
/// <see cref="DataGrid.SelectionChanged"/> event and mirrors additions/removals into a bound
/// <see cref="IList"/> — in practice
/// <c>ObservableCollection&lt;MusicTag.App.ViewModels.FileListItemViewModel&gt;</c> on
/// <c>MainWindowViewModel</c> — one item at a time, so the view model always reflects exactly
/// what's currently selected in the grid.
///
/// Deliberately one-directional (grid -> view model): nothing in this app programmatically
/// drives the grid's selection from the view model. Folder-open/Refresh clears
/// <c>MainWindowViewModel.Files</c> (the grid's ItemsSource), which the grid itself turns
/// into a <see cref="DataGrid.SelectionChanged"/> with everything in
/// <see cref="SelectionChangedEventArgs.RemovedItems"/> — so the bound collection empties out
/// through this same path rather than needing an explicit reverse sync.
/// </summary>
public sealed class DataGridSelectedItemsBehavior : Behavior<DataGrid>
{
    public static readonly DependencyProperty SelectedItemsProperty =
        DependencyProperty.Register(
            nameof(SelectedItems),
            typeof(IList),
            typeof(DataGridSelectedItemsBehavior),
            new PropertyMetadata(null));

    public IList? SelectedItems
    {
        get => (IList?)GetValue(SelectedItemsProperty);
        set => SetValue(SelectedItemsProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.SelectionChanged += OnSelectionChanged;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.SelectionChanged -= OnSelectionChanged;
        base.OnDetaching();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
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
