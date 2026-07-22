namespace MusicTag.App.ViewModels;

/// <summary>
/// Non-generic marker so a single value converter (<see cref="Converters.MixedValuePlaceholderConverter"/>)
/// can inspect <see cref="IsMixed"/> without knowing the closed generic type
/// (<see cref="MixedValue{T}"/> is instantiated once per field type — string? for most tag
/// fields, int? for Year/Track#/Disc#).
/// </summary>
public interface IMixedValue
{
    bool IsMixed { get; }
}

/// <summary>
/// Per-field distinctness snapshot across the current grid selection, per plan section 5:
/// "N selected → per-field MixedValue&lt;T&gt; struct (IsMixed + Value) computed by
/// distinctness across the selection". A <c>record struct</c> gives free structural equality
/// for the same reason <see cref="MusicTag.Core.Models.TagFieldSet"/> is a record — not
/// strictly required here (nothing currently diffs two MixedValue instances against each
/// other), but keeps the type consistent with the rest of the codebase's "let equality do the
/// work" style.
///
/// <see cref="EditPanelViewModel"/> only ever *reads* distinctness from this struct to decide
/// what to show (real value when unanimous, blank + a "&lt;keep&gt;" placeholder when mixed —
/// see <see cref="Converters.MixedValuePlaceholderConverter"/>); the actual editable text-box
/// value the user types into remains a plain nullable property (e.g. EditPanelViewModel.Title)
/// so committing an edit stays simple two-way data binding instead of a bespoke two-way
/// converter that would have to guess whether a typed value was "still mixed."
/// </summary>
/// <remarks>Deliberately <c>T Value</c>, not <c>T?</c>: this type is always instantiated with
/// an already-nullable closed type (<c>MixedValue&lt;string?&gt;</c>, <c>MixedValue&lt;int?&gt;</c>),
/// and applying <c>?</c> to an unconstrained type parameter that a caller might close over a
/// nullable value type (<c>int?</c>) is unnecessary here — the type argument itself already
/// carries the nullability.</remarks>
public readonly record struct MixedValue<T>(bool IsMixed, T Value) : IMixedValue
{
    public static readonly MixedValue<T> Empty = new(false, default!);

    /// <summary>Computes distinctness across <paramref name="values"/> using the default
    /// equality comparer for <typeparamref name="T"/> (e.g. <c>string?</c>/<c>int?</c> both
    /// compare by value, matching how <see cref="MusicTag.Core.Models.TagFieldSet"/>'s record
    /// equality already treats these fields). An empty selection (0 files) is reported as
    /// "not mixed, no value" — the same shape as a single file whose value happens to be
    /// null, which is exactly the blank/disabled state 0-selection should show.</summary>
    public static MixedValue<T> From(IEnumerable<T> values)
    {
        using var enumerator = values.GetEnumerator();
        if (!enumerator.MoveNext())
            return Empty;

        var first = enumerator.Current;
        while (enumerator.MoveNext())
        {
            if (!EqualityComparer<T>.Default.Equals(enumerator.Current, first))
                return new MixedValue<T>(true, default!);
        }

        return new MixedValue<T>(false, first);
    }
}
