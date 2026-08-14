%global dotnet_version 8.0
# A self-contained `dotnet publish` output has no native C/C++ debug sources for rpmbuild's
# find-debuginfo to collect — without this, %install leaves an empty debugsourcefiles.list and
# the auto-generated -debugsource subpackage fails with "Empty %files file".
%global debug_package %{nil}

# The self-contained publish output ships libcoreclrtraceptprovider.so, which links against
# liblttng-ust.so.0 for optional LTTng diagnostics tracing. It's dlopen'd lazily by coreclr only
# if a tracing session is explicitly requested, so its absence isn't fatal to the app — but
# rpmbuild's automatic dependency scanner still emits a hard Requires from its ELF NEEDED entry.
# Fedora 44's lttng-ust (2.14) only ships the newer liblttng-ust.so.1 SONAME, so the auto-generated
# requirement is unsatisfiable there; exclude it rather than pull in a compat package that doesn't
# exist on this release.
%global __requires_exclude ^liblttng-ust\\.so\\.0.*$

# MusicTag Fedora RPM — Linux counterpart of installer/MusicTag.iss's Windows Inno Setup
# installer. Packages a self-contained `dotnet publish` (Avalonia app + its own .NET runtime,
# so users don't need dotnet installed) rather than building against Fedora's system dotnet
# packages the way an official Fedora repo submission eventually would — self-contained is the
# simpler, more portable path for a third-party RPM distributed outside Fedora's own repos.
#
# Unlike the Windows installer's [Run] section, this spec's %post deliberately does NOT call
# `musictag --register-file-manager` automatically. RPM %post scriptlets run as root during
# `dnf install`, not as the desktop user — LinuxFileManagerIntegrationService.Register() writes
# to the *invoking user's* home directory (~/.local/share/...), so running it from %post would
# register the integration for root, not for whichever user actually uses the app. Registering
# the file-manager integration is therefore a manual, per-user opt-in via the app's own
# Settings window (the "File manager integration" toggle), exactly like on Windows the setting
# is user-controllable — only the *install-time automation* differs, for a real reason.
#
# Build: see installer/fedora/build-rpm.sh, which stages a source tarball and runs
# `rpmbuild -ba musictag.spec`. Requires network access during %build (NuGet restore), same as
# any `dotnet publish`/`dotnet restore` invocation — not vendored for offline/mock builds.

Name:           musictag
Version:        1.15.0
Release:        1%{?dist}
Summary:        Audio file tag editor (Mp3tag-style)

License:        MIT
URL:            https://github.com/FarPrince/MP3Tag
Source0:        %{name}-%{version}.tar.gz

BuildRequires:  dotnet-sdk-%{dotnet_version}
Requires:       (dotnet-runtime-%{dotnet_version} or true)
ExclusiveArch:  x86_64

%description
MusicTag is an open-source, Mp3tag-style audio tag editor: batch-edit ID3/Vorbis/APE/etc. tags
and album art across 34 audio/container formats (MP3, FLAC, OGG, M4A, WAV, WMA, APE, MPC, WV,
TTA, TAK, OptimFROG, and more), with undo/redo, multi-select batch editing, and LRCLib lyrics
lookup. Built with Avalonia UI (.NET 8) — the same codebase runs on Windows and Linux.

%prep
%setup -q

%build
dotnet publish src/MusicTag.App/MusicTag.App.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishReadyToRun=false \
    -o %{_builddir}/%{name}-%{version}/publish

%install
rm -rf %{buildroot}

install -d %{buildroot}%{_libdir}/%{name}
cp -a %{_builddir}/%{name}-%{version}/publish/. %{buildroot}%{_libdir}/%{name}/

install -d %{buildroot}%{_bindir}
ln -s %{_libdir}/%{name}/MusicTag %{buildroot}%{_bindir}/%{name}

install -Dm644 installer/fedora/musictag.desktop %{buildroot}%{_datadir}/applications/%{name}.desktop
install -Dm644 src/MusicTag.App/Assets/logo.png %{buildroot}%{_datadir}/icons/hicolor/128x128/apps/%{name}.png

%files
%{_libdir}/%{name}/
%{_bindir}/%{name}
%{_datadir}/applications/%{name}.desktop
%{_datadir}/icons/hicolor/128x128/apps/%{name}.png

%post
update-desktop-database %{_datadir}/applications &> /dev/null || :
gtk-update-icon-cache %{_datadir}/icons/hicolor &> /dev/null || :

%postun
update-desktop-database %{_datadir}/applications &> /dev/null || :
gtk-update-icon-cache %{_datadir}/icons/hicolor &> /dev/null || :

%changelog
* Fri Aug 14 2026 FarPrince <noreply@example.com> - 1.15.0-1
- Version 1.15: fix column resize/drag-reorder (Avalonia.Controls.DataGrid's resize/reorder
  properties default to disabled, unlike WPF), rename collisions on Linux's case-sensitive
  filesystems, Nautilus Scripts multi-selection, and the album-art right-click menu (both
  rendering nothing under FluentAvaloniaTheme and, once switched to MenuFlyout, opening far from
  the cursor — replaced with a positioned owner window hosting a real MenuFlyoutPresenter).
  Rewrote clipboard image copy/paste onto Avalonia's newer DataFormat.Bitmap API. Hid the
  Windows-only Acrylic/Mica backdrop picker on Linux.

* Thu Aug 13 2026 FarPrince <noreply@example.com> - 1.14.0-1
- Version 1.14: fix DataGrid column headers, gridlines, row selection/hover, and invalid-cell
  indicators rendering invisible on Linux (missing SystemXxxColor resources backfilled in
  App.axaml, since FluentAvaloniaTheme doesn't define the classic UWP-style keys that
  Avalonia.Controls.DataGrid's bundled Fluent.xaml theme depends on).

* Wed Aug 05 2026 FarPrince <noreply@example.com> - 1.13.0-1
- Version 1.13: system accent color (PreferUserAccentColor via the freedesktop portal), Fedora
  packaging fixes (build-rpm.sh version regex, liblttng-ust.so.0 requires-exclude).

* Wed Aug 05 2026 FarPrince <noreply@example.com> - 1.12.0-1
- Version 1.12.

* Tue Aug 04 2026 FarPrince <noreply@example.com> - 1.11.0-1
- Initial Fedora packaging of the Avalonia (cross-platform) port.
