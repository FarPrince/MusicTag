# MusicTag

Open-source recreation of Mp3tag: a cross-platform (Windows + Linux) desktop audio tag editor.
.NET 8 + [Avalonia UI](https://avaloniaui.net/) 11.3 (FluentAvaloniaTheme for Fluent-style
theming/Acrylic-Mica backdrops; `Avalonia.Controls.DataGrid` for the file grid) +
[ATL.NET](https://github.com/Zeugma440/atldotnet) (tag read/write across 34
audio/container formats, including obscure lossless codecs: APE, MPC, WV, TTA, TAK,
OptimFROG). MVVM via `CommunityToolkit.Mvvm` ([ObservableProperty]/[RelayCommand]
source generators). Originally built as a WPF/WPF-UI Windows-only app; rewritten onto Avalonia
so the same `MusicTag.App` project builds and runs on both Windows and Linux (Fedora is the
primary Linux packaging target — see `installer/fedora/`).

## Build / test / run

```
dotnet build
dotnet test
dotnet run --project src/MusicTag.App
```

Cross-platform (Windows and Linux; nothing in `MusicTag.App` is Windows-only anymore — see
"File manager integration" below for the one place that still branches by OS). On Linux, if
`dotnet build` fails with a file-lock error on the `MusicTag` binary, a previous test instance
is still running — `pkill -f 'bin/.*/MusicTag$'`, or just retry the build. On Windows, check
`tasklist //FI "IMAGENAME eq MusicTag.exe"` / `taskkill //PID <pid> //F` the same as before.

Verifying a UI change actually works requires launching the app, same as always — on a
headless Linux box (e.g. this dev container) that means Xvfb: `Xvfb :99 -screen 0 1280x800x24 &`,
then `DISPLAY=:99 dotnet run --project src/MusicTag.App -- <folder>` and
`DISPLAY=:99 import -window root screenshot.png` (ImageMagick) to inspect it, `xdotool` to
simulate clicks/typing. A real bug was caught exactly this way during the Avalonia port (see
App.axaml.cs's own doc comment on `mainWindow.Show()`) that pure code review missed.

## Solution layout

- `src/MusicTag.App` — Avalonia executable: Views (`.axaml` + code-behind), ViewModels,
  Controls (`AlbumArtControl`), Behaviors (`Avalonia.Xaml.Interactivity` — clipboard paste,
  multi-select sync), Services (thin Avalonia-facing wrappers: dialogs, file picker, theme),
  Converters.
- `src/MusicTag.Core` — all tagging/domain/undo-redo logic, **no UI-framework reference** —
  stays fast to unit test (no UI-thread test host) and reusable from a future CLI tool. Plain
  `net8.0`, builds and tests identically on Windows/Linux/macOS.
- `src/MusicTag.Tests` — xUnit tests against `MusicTag.Core` only.
- `test-assets/` — small sample audio files per format, used by the automated tests. **Never
  leave these mutated** — manually launching the app against `test-assets/` (e.g. to verify a
  UI change) edits and auto-saves tags on the real files on disk; check `git status
  test-assets/` afterward and `git checkout -- test-assets/` if anything shows modified.
- `installer/MusicTag.iss` — Windows installer (Inno Setup).
- `installer/fedora/` — Linux installer: `musictag.spec` (RPM, self-contained `dotnet publish`)
  + `build-rpm.sh` (stages a source tarball via `git archive` and runs `rpmbuild -ba`).

## Key architectural decisions (why, not just what)

- **Tag-field and album-art edits are pending in-memory (`AudioFile.PendingFields` /
  `PendingAlbumArt`) until Ctrl+S** — but **filename edits rename on disk immediately**
  (`IAudioFileService.Rename` → `File.Move`), matching real Mp3tag. This is why
  `RenameCommand` is the one `IEditCommand` whose `Do()`/`Undo()` can fail for reasons
  outside the app's control (collision, lock, permissions) — it goes through
  `EditHistory.TryExecute`/`TryUndo`/`TryRedo` (fallible), while every other edit uses
  the plain `Execute` (cannot fail). Don't add fallible variants for other command types
  — this asymmetry is intentional and should stay localized to rename.
- **Undo/redo is session-only** (in-memory `EditHistory`, cleared on app close) — Save
  does NOT clear it; undoing after a save re-marks the file dirty. Opening a folder or
  Refresh (F5) MUST call `EditHistory.Clear()` first (with a discard-confirmation dialog
  if anything is dirty) — old commands hold direct `AudioFile` references that become
  stale on rescan.
- **Multi-selection batch edits** collapse to one `CompositeEditCommand` (one undo step
  for N files). `EditPanelViewModel`'s `MixedValue<T>` shows `<keep>` via the TextBox's
  `Watermark` (not real `Text`) when the selection disagrees on a field, so an
  untouched mixed field commits nothing.
- **Extension aliasing**: `ExtensionParserResolver` maps `.mkv/.mk3d/.apl/.flc` (which
  ATL doesn't register by default) to the canonical extension whose parser it shares
  (`.mka`/`.ape`/`.flac`) via ATL's `Track(Stream, string mimeType)` constructor — the
  dot-prefixed string is the load-bearing part, independent of the real filename.
- **File manager integration is the one place App.axaml.cs/Program.cs branch on
  `OperatingSystem.IsWindows()`** — both implementations share the single
  `IExplorerIntegrationService` interface (`Register()`/`Unregister()`/`IsRegistered()`):
  `ExplorerIntegrationService` (Windows, HKCU registry, behind `IRegistryKeyWrapper` so tests
  assert exact key/value strings without touching the real registry) vs.
  `LinuxFileManagerIntegrationService` (Linux, a per-user `~/.local/share/applications/musictag
  .desktop` entry + a Nautilus script in `~/.local/share/nautilus/scripts`, behind
  `ILinuxDesktopFileWriter` for the same fake-filesystem testability). Both are HKCU/home-
  directory-only — no elevation needed either way. **The Fedora RPM's `%post` does NOT
  auto-register this** (unlike the Windows installer's `[Run]` step) — RPM `%post` runs as
  root during `dnf install`, not as the desktop user, so it would register the integration for
  the wrong account; it stays a manual per-user opt-in via the Settings window's toggle on
  Linux, same UI either OS.

## Avalonia-porting notes (read before touching XAML/code-behind)

The app was ported from WPF to Avalonia; most of it is a close analogue, but a few real API
differences bit during the port and are worth knowing before assuming WPF muscle memory holds:

- **`DataGridColumn` doesn't support `x:Name` field generation.** It's a plain
  `AvaloniaObject`, not a `StyledElement`, so `x:Name` on a `<DataGridTextColumn>`/
  `<DataGridTemplateColumn>` is accepted by the XAML compiler but silently produces no
  code-behind field (confirmed by hitting the resulting `CS0103` errors, not by inspection).
  MainWindow.axaml gives every column a `Tag="XyzColumn"` string instead; MainWindow.axaml.cs
  resolves them once in the constructor via `FileGrid.Columns.First(c => Equals(c.Tag, tag))`
  into regular `private readonly DataGridColumn` fields, so the rest of that file reads
  exactly like the WPF original.
- **No `DataGridCellInfo`.** Avalonia's DataGrid tracks "current cell" as two independent
  properties, `SelectedItem` (`object?`) and `CurrentColumn` (`DataGridColumn?`), set together
  — there's no combined struct to construct.
- **`DataGridColumn.IsVisible` is a plain `bool`**, not a WPF-style `Visibility` enum (Avalonia
  has no Collapsed/Hidden distinction anywhere) — every control's visibility, and this
  non-control column property too, is just `IsVisible`.
- **`Window.Position` is `PixelPoint` (physical pixels)**, separate from `Width`/`Height`
  (logical/DIP units) — converting between the two needs `Window.RenderScaling`. There's no
  `Window.RestoreBounds`; capture whatever `Width`/`Height`/`Position` are before maximizing
  instead. `SystemParameters.VirtualScreen*` has no Avalonia equivalent — use
  `TopLevel.Screens.Primary`/`.All` (each a `PixelRect` `Bounds`/`WorkingArea`).
- **`TextBox` bindings DO support `UpdateSourceTrigger=LostFocus`** (`Avalonia.Data.Binding
  .UpdateSourceTrigger`, same enum member names as WPF) — this is easy to assume doesn't exist
  since it's not the obvious/default Avalonia binding story, but it does, and the commit-on-
  Enter code-behind pattern (`BindingOperations.GetBindingExpressionBase(textBox,
  TextBox.TextProperty)?.UpdateSource()`) ports 1:1 from WPF's `GetBindingExpression(...)
  .UpdateSource()`.
- **Modal dialogs are async-only.** `Window.ShowDialog(Window owner)` returns `Task` (there is
  no blocking overload, unlike WPF's synchronous `ShowDialog()`) — `IDialogService`'s methods
  are all `Task`-returning as a result, and every call site awaits them (several WPF-era
  synchronous `[RelayCommand] private void X()` methods became `private async Task X()`).
- **Clipboard image read/write has no typed `GetImage()`/`SetImage()`.** `IClipboard` only
  exposes raw MIME-format-string/object pairs (`GetFormatsAsync`/`GetDataAsync(string)`/
  `SetDataObjectAsync`); which format string a real desktop clipboard offers for an image is
  compositor/toolkit-dependent (see `Behaviors/ClipboardImageHelper.cs`'s format list) — this
  is a genuine, not-fully-solved platform difference from WPF's clipboard, most noticeable on
  Linux where clipboard managers vary more than on Windows.
- **Two pieces of MainWindow's original WPF DataGrid logic were deliberately dropped, not
  ported** (see MainWindow.axaml.cs's class doc comment for the full reasoning): the Star-
  column resize-cascade guard (a workaround for a specific WPF DataGrid proportional-resize
  quirk with no confirmed Avalonia equivalent) and the double-click-to-edit workaround for
  `DataGridTemplateColumn` (Avalonia's `DataGridCell` doesn't expose the public Column/
  IsEditing surface that workaround needed). F2 remains a fully reliable way to start a
  rename either way. Similarly, the custom `DataGridRow` `ControlTemplate` replacement (WPF-UI
  theme bug workaround) was replaced with a much smaller scoped `Style`+`Binding` that only
  handles the dirty-row italic indicator — selection/hover/alternating-row visuals are left at
  FluentAvaloniaTheme's own DataGrid theme defaults rather than re-derived from scratch.
- **Window chrome**: the WPF original extended content into a custom title bar
  (`ExtendsContentIntoTitleBar` + WPF-UI's `ui:TitleBar`). This port deliberately uses plain OS
  window decorations instead — Avalonia's `ExtendClientAreaToDecorationsHint` is known to
  render inconsistently across Linux window managers/compositors, and getting it wrong on the
  one platform this rewrite exists to support was a worse risk than a plain title bar.

## Conventions

- No code comments explaining *what* code does — only non-obvious *why* (a hidden
  constraint, a workaround, a past bug). This codebase's existing comments follow that
  rule; match it.
- Every non-trivial UI/behavior change should be verified live (build, run tests, then
  actually launch the app and check pixel/behavior state), not just reviewed by reading
  the code — see "Build / test / run" above for how to do this headlessly on Linux. Several
  past "fixes" in this repo (both the WPF original and the Avalonia port) turned out to be
  partial until measured empirically.
- Before killing or launching a test instance, check for one already running first (`tasklist`
  on Windows, `pgrep -af MusicTag` on Linux) — the user may already have one open to look at
  the thing you just changed.
