#!/usr/bin/env bash
#
# Builds the JingleBox2 Flatpak from this checkout.
#
#   packaging/flatpak/build.sh sources   writes nuget-sources.json beside the manifest
#   packaging/flatpak/build.sh version   writes the newest tag's version beside the manifest and
#                                        adds it to the metainfo's releases if it is not there
#   packaging/flatpak/build.sh build     builds the Flatpak and installs it for this user
#   packaging/flatpak/build.sh bundle    builds it and writes a single jinglebox2.flatpak file
#
# There is no git inside a Flatpak build, so the version cannot be read the way the csproj reads
# it. "version" takes it from the newest tag, here where git is, and writes it to a file the
# manifest's publish reads. build and bundle do that first, and the CI workflow runs the same
# step, so a local build and a CI one cannot disagree about which version they are.
#
# A Flatpak is built with no network, so every NuGet package the publish needs is listed in
# nuget-sources.json with its checksum, and flatpak-builder downloads them before the build
# starts. Run "sources" again whenever a PackageReference changes.
#
# Needs flatpak, the Flathub remote, and flatpak-builder (or the org.flatpak.Builder app). The
# first run installs org.freedesktop.Sdk 25.08 and the .NET 10 SDK extension, about a gigabyte.

set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
APP_ID="io.github.petervdpas.JingleBox2"
MANIFEST="$HERE/$APP_ID.yml"
SOURCES="$HERE/nuget-sources.json"
VERSION_FILE="$HERE/version"
FREEDESKTOP="25.08"
DOTNET="10"
GENERATOR_URL="https://raw.githubusercontent.com/flatpak/flatpak-builder-tools/master/dotnet/flatpak-dotnet-generator.py"
WORK="${JB_FLATPAK_WORK:-$ROOT/.flatpak-work}"
# Where the SDK goes: --user here, --system in the CI container, which is set up that way.
SCOPE="${JB_FLATPAK_SCOPE:---user}"

builder() {
  if command -v flatpak-builder >/dev/null 2>&1; then
    flatpak-builder "$@"
  else
    flatpak run org.flatpak.Builder "$@"
  fi
}

sdk() {
  flatpak install "$SCOPE" --noninteractive flathub \
    "org.freedesktop.Platform//$FREEDESKTOP" \
    "org.freedesktop.Sdk//$FREEDESKTOP" \
    "org.freedesktop.Sdk.Extension.dotnet$DOTNET//$FREEDESKTOP"
  if ! command -v flatpak-builder >/dev/null 2>&1; then
    flatpak install "$SCOPE" --noninteractive flathub org.flatpak.Builder
  fi
}

sources() {
  sdk
  mkdir -p "$WORK"
  curl -fsSL -o "$WORK/flatpak-dotnet-generator.py" "$GENERATOR_URL"

  # The generator restores into a folder of its own and ignores a failed restore, so an empty
  # or short list is checked for here rather than found out by a build that cannot restore.
  #
  # The output file and the project come first. --runtime takes any number of values, so written
  # after it they are read as two more runtimes and the generator says both are missing; and
  # --dotnet-args takes everything after it, so it stays last. RuntimeIdentifiers is emptied there,
  # the same as in the manifest's publish, so each restore is for the one runtime it is given
  # rather than for the win-x64 the csproj also lists.
  (cd "$ROOT" && python3 "$WORK/flatpak-dotnet-generator.py" \
    "$SOURCES" \
    JingleBox2.csproj \
    --freedesktop "$FREEDESKTOP" \
    --dotnet "$DOTNET" \
    --runtime linux-x64 linux-arm64 \
    --dotnet-args -p:SelfContained=true -p:RuntimeIdentifiers=)

  local count
  count="$(python3 -c 'import json,sys; print(len(json.load(open(sys.argv[1]))))' "$SOURCES")"
  for pack in microsoft.netcore.app.runtime.linux-x64 microsoft.netcore.app.runtime.linux-arm64; do
    if ! grep -q "/$pack/" "$SOURCES"; then
      echo "ERROR: $pack is not in $SOURCES, so a self-contained publish cannot restore offline"
      exit 1
    fi
  done
  echo "OK: $count packages in $SOURCES"
}

version() {
  local tag
  if ! tag="$(git -C "$ROOT" describe --tags --abbrev=0 2>/dev/null)"; then
    echo "ERROR: this checkout has no tag, so there is no version to build the Flatpak as"
    exit 1
  fi
  tag="${tag#v}"
  if ! [[ "$tag" =~ ^[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z]+)*$ ]]; then
    echo "ERROR: the newest tag, $tag, is not a version"
    exit 1
  fi
  printf '%s\n' "$tag" > "$VERSION_FILE"
  echo "OK: building as $tag"

  # The release list in the metainfo is what a software centre shows and what Flathub's checker
  # reads, and a tag is the only thing that makes a release, so a tag missing from it is added
  # here, newest first and dated the day the tag was made. Already listed is left alone, so running
  # this twice adds nothing.
  local metainfo="$HERE/$APP_ID.metainfo.xml"
  if ! grep -q "<release version=\"$tag\"" "$metainfo"; then
    local when
    when="$(git -C "$ROOT" log -1 --format=%cs "v$tag" 2>/dev/null || git -C "$ROOT" log -1 --format=%cs "$tag")"
    sed -i "s|^  <releases>\$|  <releases>\n    <release version=\"$tag\" date=\"$when\"/>|" "$metainfo"
    if grep -q "<release version=\"$tag\"" "$metainfo"; then
      echo "OK: added $tag ($when) to the releases in $APP_ID.metainfo.xml"
    else
      echo "WARNING: could not add $tag to the releases in $APP_ID.metainfo.xml"
    fi
  fi
}

build() {
  version
  [ -f "$SOURCES" ] || sources
  sdk
  builder --user --install --force-clean \
    --state-dir="$WORK/state" \
    --install-deps-from=flathub \
    "$WORK/build" "$MANIFEST"
  echo "Installed. Run it with: flatpak run $APP_ID"
}

bundle() {
  version
  [ -f "$SOURCES" ] || sources
  sdk
  builder --user --force-clean \
    --state-dir="$WORK/state" \
    --install-deps-from=flathub \
    --repo="$WORK/repo" \
    "$WORK/build" "$MANIFEST"
  flatpak build-bundle \
    --runtime-repo=https://dl.flathub.org/repo/flathub.flatpakrepo \
    "$WORK/repo" "$ROOT/jinglebox2.flatpak" "$APP_ID"
  echo "Wrote $ROOT/jinglebox2.flatpak"
}

case "${1:-build}" in
  sources) sources ;;
  version) version ;;
  build) build ;;
  bundle) bundle ;;
  *)
    echo "usage: $0 [sources|version|build|bundle]"
    exit 2
    ;;
esac
