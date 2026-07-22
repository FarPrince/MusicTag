namespace MusicTag.Core.History;

/// <summary>
/// Wraps N leaf commands (<see cref="FieldEditCommand"/>/<see cref="AlbumArtEditCommand"/>,
/// or future leaf types) as one undoable unit, so a single <see cref="EditHistory.Execute"/>
/// call pushes exactly one stack entry regardless of how many files were affected. Per plan
/// section 4: "Batch edits become a single undoable unit naturally: when N files are selected
/// and the user commits a tag-field or album-art edit, EditPanelViewModel/AlbumArtViewModel
/// build one leaf command per affected file ... and wrap them in one CompositeEditCommand
/// pushed via a single Execute call. A single-file edit is just the N==1 case." — this
/// milestone (M3) only ever constructs these with exactly one child (single-selection editing
/// only); M5 is what starts constructing them with N>1 children.
/// </summary>
public sealed class CompositeEditCommand : IEditCommand
{
    private readonly IReadOnlyList<IEditCommand> _commands;

    public CompositeEditCommand(string description, IReadOnlyList<IEditCommand> commands)
    {
        if (commands.Count == 0)
            throw new ArgumentException("CompositeEditCommand requires at least one child command.", nameof(commands));

        Description = description;
        _commands = commands;
    }

    public string Description { get; }

    public void Do()
    {
        foreach (var command in _commands)
        {
            command.Do();
        }
    }

    /// <summary>Undoes children in reverse order, matching the plan's spec verbatim — matters
    /// once a leaf command's Undo() can depend on a later one having already been undone
    /// (not the case for the pure in-memory field/art commands today, but the ordering
    /// guarantee is part of this class's contract regardless).</summary>
    public void Undo()
    {
        for (var i = _commands.Count - 1; i >= 0; i--)
        {
            _commands[i].Undo();
        }
    }
}
