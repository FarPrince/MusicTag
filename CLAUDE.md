# MusicTag

Open-source recreation of Mp3tag: a Windows desktop audio tag editor. .NET 8 WPF +
[WPF-UI](https://github.com/lepoco/wpfui) (Fluent/Mica/Acrylic theming) +
[ATL.NET](https://github.com/Zeugma440/atldotnet) (tag read/write across 34
audio/container formats, including obscure lossless codecs: APE, MPC, WV, TTA, TAK,
OptimFROG). MVVM via `CommunityToolkit.Mvvm` ([ObservableProperty]/[RelayCommand]
source generators). The full design plan (milestones M0-M9) lives at
`C:\Users\vedha\.claude\plans\i-want-an-open-quizzical-starlight.md`.

## Build / test / run

```
dotnet build
dotnet test
dotnet run --project src/MusicTag.App
```

Windows-only (WPF). If `dotnet build` fails with a file-lock error on
`MusicTag.App.exe`/`.dll`, a previous test instance of the app is still running —
`tasklist //FI "IMAGENAME eq MusicTag.App.exe"` then `taskkill //PID <pid> //F`, or just
retry the build (locks from antivirus/indexer scans are often transient).

## Solution layout

- `src/MusicTag.App` — WPF executable: Views (XAML + code-behind), ViewModels,
  Controls (`AlbumArtControl`), Behaviors (Microsoft.Xaml.Behaviors.Wpf — clipboard
  paste, multi-select sync), Services (thin WPF-facing wrappers: dialogs, file picker,
  theme), Converters, Styles.
- `src/MusicTag.Core` — all tagging/domain/undo-redo logic, **no WPF reference** — stays
  fast to unit test (no STA/WPF test host) and reusable from a future CLI tool.
- `src/MusicTag.Tests` — xUnit tests against `MusicTag.Core` only.
- `test-assets/` — small sample audio files per format, used by the automated tests.

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
  for N files). `EditPanelViewModel`'s `MixedValue<T>` shows `<keep>` via
  `PlaceholderText` (not real `Text`) when the selection disagrees on a field, so an
  untouched mixed field commits nothing.
- **Extension aliasing**: `ExtensionParserResolver` maps `.mkv/.mk3d/.apl/.flc` (which
  ATL doesn't register by default) to the canonical extension whose parser it shares
  (`.mka`/`.ape`/`.flac`) via ATL's `Track(Stream, string mimeType)` constructor — the
  dot-prefixed string is the load-bearing part, independent of the real filename.
- **Explorer integration** is HKCU-only (no elevation) — `IRegistryKeyWrapper`
  indirection exists specifically so tests can assert exact key/value strings without
  touching the real registry.

## WPF gotchas hit in this codebase (don't re-introduce)

- **Self-referencing `BasedOn="{StaticResource {x:Type T}}"` styles silently fail to
  resolve when declared in the same `ResourceDictionary` that also merges in the base
  style via `MergedDictionaries`.** Any override of a WPF-UI control style (see
  `Styles/ButtonOverrides.xaml`) must live in its own file, merged in *after* the
  library's dictionary in `App.xaml` — not declared inline alongside the merge.
- **A `Style.Trigger` cannot neutralize a `TargetName`d part's `Visibility` toggle
  sealed inside an inherited `ControlTemplate`.** Overriding the brush a built-in part
  uses only hides its color, not its layout footprint. `MainWindow.xaml`'s custom
  `DataGridRow` style therefore owns a full replacement `ControlTemplate` (plain WPF
  `Border`/`SelectiveScrollingGrid` structure) rather than trying to patch WPF-UI's
  default one.
- UI Automation quirk (only matters for automated/manual testing, not app code):
  `TogglePattern.Toggle()` on a checkable `MenuItem` flips `IsChecked` without firing the
  real WPF `Click` event — don't rely on it to test click-driven behavior.

## Conventions

- No code comments explaining *what* code does — only non-obvious *why* (a hidden
  constraint, a workaround, a past bug). This codebase's existing comments follow that
  rule; match it.
- Every non-trivial UI/behavior change should be verified live (build, run tests, then
  actually launch the app and check pixel/behavior state), not just reviewed by reading
  the code — several past "fixes" in this repo turned out to be partial until measured
  empirically (e.g. a row-selection indicator whose color was fixed but whose layout
  footprint still shifted text by a few pixels).
- Before killing or launching a `MusicTag.App.exe` test instance, check
  `tasklist //FI "IMAGENAME eq MusicTag.App.exe"` first — the user may already have one
  open to look at the thing you just changed.
