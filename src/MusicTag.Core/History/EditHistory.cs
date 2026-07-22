namespace MusicTag.Core.History;

/// <summary>
/// Session-only (in-memory, cleared on app close or explicit <see cref="Clear"/>) undo/redo
/// stack, exactly per plan section 4. Ordinary tag-field/album-art edits are pushed via the
/// plain <see cref="Execute"/>, which cannot fail (those commands are pure in-memory).
/// <see cref="TryExecute"/>/<see cref="TryUndo"/>/<see cref="TryRedo"/> exist only for command
/// types that can fail for reasons outside the app's control — real disk I/O — which as of
/// this milestone means none are wired yet (RenameCommand, the one such type, is M4). They're
/// implemented now regardless so M4 doesn't need to touch this class at all.
///
/// Deliberately NOT cleared by Save — undo after a save is intentionally allowed (it reverts
/// the in-memory field and re-marks the file dirty; a subsequent Save would rewrite it). Only
/// opening a folder or Refresh clears it, since old commands hold direct references to
/// AudioFile instances that get discarded/replaced on rescan.
/// </summary>
public sealed class EditHistory
{
    private readonly Stack<IEditCommand> _undo = new();
    private readonly Stack<IEditCommand> _redo = new();

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>Description of the command that a Ctrl+Z would currently undo, or null if
    /// the undo stack is empty. Drives the status-bar "top-of-undo-stack description" called
    /// for in plan section 5 — not part of the plan's literal EditHistory code block, but a
    /// direct, non-breaking addition (a read-only property) needed to satisfy that
    /// requirement without reaching into the stack from outside the class.</summary>
    public string? TopUndoDescription => _undo.Count > 0 ? _undo.Peek().Description : null;

    /// <summary>Raised after any successful Execute/TryExecute/TryUndo/TryRedo/Clear so
    /// bound UI (toolbar Undo/Redo enabled state, status bar description) can refresh.</summary>
    public event EventHandler? Changed;

    public void Execute(IEditCommand cmd)
    {
        cmd.Do();
        _undo.Push(cmd);
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool TryExecute(IEditCommand cmd, out Exception? error)
    {
        try
        {
            cmd.Do();
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }

        _undo.Push(cmd);
        _redo.Clear();
        error = null;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool TryUndo(out Exception? error) => TryStep(_undo, _redo, c => c.Undo(), out error);

    public bool TryRedo(out Exception? error) => TryStep(_redo, _undo, c => c.Do(), out error);

    private bool TryStep(Stack<IEditCommand> from, Stack<IEditCommand> to, Action<IEditCommand> step, out Exception? error)
    {
        if (from.Count == 0)
        {
            error = null;
            return true;
        }

        var cmd = from.Peek();
        try
        {
            step(cmd);
        }
        catch (Exception ex)
        {
            // Left exactly as-is on failure — neither stack mutated, matching the plan's
            // note that an undo/redo hitting a collision must surface an error instead of
            // corrupting the stacks or silently no-op'ing.
            error = ex;
            return false;
        }

        to.Push(from.Pop());
        error = null;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
