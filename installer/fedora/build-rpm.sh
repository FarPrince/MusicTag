#!/usr/bin/env bash
# Builds the Fedora RPM for MusicTag: stages a source tarball from the repo working tree
# (git archive, so only tracked files go in — no bin/obj cruft) and runs rpmbuild against
# musictag.spec. Mirrors installer/MusicTag.iss's own two-step "publish, then package" shape,
# except the spec's own %build runs `dotnet publish` itself, so this script's only job is
# getting the source tree into ~/rpmbuild/SOURCES in the layout rpmbuild expects.
#
# Requires: rpm-build (rpmbuild) and dotnet-sdk-8.0 (both `sudo dnf install rpm-build
# dotnet-sdk-8.0` on Fedora). Network access is required during the build (NuGet restore).
#
# Usage: installer/fedora/build-rpm.sh
# Output: ~/rpmbuild/RPMS/x86_64/musictag-<version>-1.<dist>.x86_64.rpm

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"
spec_file="$script_dir/musictag.spec"

version="$(grep -oP '^Version:\s+\K\S+' "$spec_file")"
name="musictag"

rpmbuild_root="${HOME}/rpmbuild"
mkdir -p "$rpmbuild_root"/{SOURCES,SPECS,BUILD,RPMS,SRPMS}

tarball="$rpmbuild_root/SOURCES/${name}-${version}.tar.gz"
echo "Archiving $repo_root (tracked files only) -> $tarball"
git -C "$repo_root" archive --format=tar.gz --prefix="${name}-${version}/" -o "$tarball" HEAD

cp "$spec_file" "$rpmbuild_root/SPECS/"

echo "Running rpmbuild..."
rpmbuild -ba "$rpmbuild_root/SPECS/musictag.spec"

echo
echo "Done. RPM(s):"
find "$rpmbuild_root/RPMS" -name "${name}-${version}-*.rpm"
