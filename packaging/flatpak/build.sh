#!/usr/bin/env bash
#
# Builds the JingleBox2 Flatpak from this checkout.
#
#   packaging/flatpak/build.sh sources   writes nuget-sources.json beside the manifest
#   packaging/flatpak/build.sh build     builds the Flatpak and installs it for this user
#   packaging/flatpak/build.sh bundle    builds it and writes a single jinglebox2.flatpak file
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
  (cd "$ROOT" && python3 "$WORK/flatpak-dotnet-generator.py" \
    --freedesktop "$FREEDESKTOP" \
    --dotnet "$DOTNET" \
    --runtime linux-x64 linux-arm64 \
    "$SOURCES" \
    JingleBox2.csproj \
    --dotnet-args -p:SelfContained=true)

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

build() {
  [ -f "$SOURCES" ] || sources
  sdk
  builder --user --install --force-clean \
    --state-dir="$WORK/state" \
    --install-deps-from=flathub \
    "$WORK/build" "$MANIFEST"
  echo "Installed. Run it with: flatpak run $APP_ID"
}

bundle() {
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
  build) build ;;
  bundle) bundle ;;
  *)
    echo "usage: $0 [sources|build|bundle]"
    exit 2
    ;;
esac
