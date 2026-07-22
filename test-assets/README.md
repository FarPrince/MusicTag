# test-assets

Small sample audio files used by `MusicTag.Tests`, plus provenance/licensing notes for each
one. Every file here is either synthetic silence generated locally with `ffmpeg`, or a
byte-for-byte renamed copy of one of those synthetic files used to exercise
`ExtensionParserResolver`'s alias mechanism — no third-party audio content is checked into
this repo.

`ffmpeg` version used: 8.1.2-essentials_build-www.gyan.dev (already on PATH on the dev
machine; if it weren't, `winget install ffmpeg` installs it).

## Generation method (common to every ffmpeg-authored file)

Each is a 1-second silent tone generated with `ffmpeg -f lavfi -i anullsrc=...`, tagged at
encode time with `-metadata title="Test Title" -metadata artist="Test Artist" -metadata
album="Test Album"` (except where a format-specific note below says otherwise), encoded with
the codec named in the table. Sample rate is 44100 Hz except Opus, which requires 48000 Hz.

## Provenance

| File | ffmpeg codec | Notes |
|---|---|---|
| `flac/silence_tagged.flac` | `flac` | M1 sample. Synthetic silence — public domain / no rights involved. |
| `flac/silence_tagged.flc` | — | Byte-for-byte copy of `silence_tagged.flac`, renamed to `.flc` to exercise the `.flc` → `.flac` alias path. |
| `mp3/silence_tagged.mp3` | `libmp3lame` | M9. |
| `ogg/silence_tagged.ogg` | `libvorbis` (Ogg container) | M9. |
| `opus/silence_tagged.opus` | `libopus` (Ogg container), 48000 Hz | M9. |
| `m4a/silence_tagged.m4a` | `aac` (MP4 container) | M9. |
| `wma/silence_tagged.wma` | `wmav2` (ASF container) | M9. |
| `wav/silence_tagged.wav` | `pcm_s16le` (RIFF/WAV container) | M9. |
| `wv/silence_tagged.wv` | `wavpack` | M9. |
| `mka/silence_tagged.mka` | `flac` (Matroska-audio container) | M9. Natively registered by ATL — the canonical counterpart for the `.mkv`/`.mk3d` alias tests below. |
| `mkv/silence_tagged.mkv` | — | Byte-for-byte copy of `mka/silence_tagged.mka`, renamed to `.mkv` to exercise the `.mkv` → `.mka` alias path. |
| `mk3d/silence_tagged.mk3d` | — | Byte-for-byte copy of `mka/silence_tagged.mka`, renamed to `.mk3d` to exercise the `.mk3d` → `.mka` alias path. |

## Known quirk: Matroska (`.mka`/`.mkv`/`.mk3d`) Title/Album

Confirmed directly (via `ffprobe`, not guessed): ffmpeg's Matroska muxer writes the global
`title` tag somewhere ATL's Matroska reader doesn't consult, so `AudioFileService.Load`
reads `Title` back as a fallback derived from the filename instead (and, for the
stream-constructed `.mkv`/`.mk3d` alias path specifically, as an *empty string*, since a
stream has no filename to fall back to — this is a real, if minor, divergence between the
canonical path-constructed read and the aliased stream-constructed read for this one field,
documented and asserted explicitly in `ExtensionParserResolverTests`). The same applies to
`Album`, which reads back empty for both paths. This reproduces identically whether the tags
are written at Matroska container level or per-track level, so it is a source-file/library
quirk specific to Matroska-as-audio-container, not a defect in `AudioFileService`: writing a
new Title/Album through ATL itself (`SaveAsync`) and reading it back afterwards round-trips
correctly (see `AudioFileServiceTests.SaveAsync_Mka_RoundTripsMutatedTitleAndAlbum`).
`Artist` is unaffected by this quirk and round-trips correctly on the *original* files too.

## Skipped: APE / TAK / OptimFROG / Musepack

Per the project plan (section 9, risk #8): ffmpeg has no encoders for Monkey's Audio (APE),
TAK, OptimFROG, or Musepack (mpc/mp+), so none of these can be generated the same way as the
formats above. A brief, time-boxed search (one targeted web search plus a check of a
known public-domain audio test-file repository on GitHub) did not turn up a trivially
obtainable small CC0/public-domain sample for any of them. Per the plan's explicit
instruction not to spend excessive effort hunting for these, all four are left as
**best-effort/manual verification only** — `SupportedExtensions`/`ExtensionParserResolver`
still register and route `.ape`/`.apl` (and `.tak`/`.ofr`/`.ofs`/`.mpc`/`.mp+`, which don't
even need alias resolution) through ATL's shared APEv2 tag engine, the same engine
exercised end-to-end by the WavPack (`.wv`) and FLAC (`.flac`/`.flc`) automated tests above,
which gives reasonable confidence by similarity — but there is no automated test asset or
test case for these four specifically. A user with a real sample file for any of them can
drop it in a same-named subfolder here and extend `AudioFileServiceTests`/
`ExtensionParserResolverTests` accordingly; no code changes would be needed.
