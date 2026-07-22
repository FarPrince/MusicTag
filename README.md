# MusicTag

An open-source recreation of Mp3tag: a Windows desktop audio tag editor built on
.NET 8 (WPF), [WPF-UI](https://github.com/lepoco/wpfui) for Fluent/Mica theming, and
[ATL.NET](https://github.com/Zeugma440/atldotnet) for tag reading/writing across a wide
range of audio/container formats (including several obscure lossless codecs such as
APE, MPC, WV, TTA, TAK, and OptimFROG).

## Solution layout

- `src/MusicTag.App` — the WPF executable (views, view-models, DI wiring).
- `src/MusicTag.Core` — class library with all tagging/domain logic, no WPF
  dependency, so it stays fast to unit test and reusable from a future CLI tool.
- `src/MusicTag.Tests` — xUnit tests for `MusicTag.Core`.
- `test-assets/` — small sample audio files used by the automated tests.

## Building

```
dotnet build
```

## Running

```
dotnet run --project src/MusicTag.App
```

## Testing

```
dotnet test
```

## Status

Under active development. See the project plan for the milestone breakdown.
