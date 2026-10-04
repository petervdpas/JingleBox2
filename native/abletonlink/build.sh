#!/usr/bin/env bash
# Builds Ableton Link's C wrapper for the three runtimes this application ships, and puts each
# library beside BASS in native/<rid>/. Needs docker and nothing else.
#
#   native/abletonlink/build.sh
#
# The release is pinned here and moving it is a decision: Link is on the timing path, and a new
# version is something to hear against Live before it ships. The asio commit is the one that
# release's modules/asio-standalone submodule points at, since a release tarball does not carry
# its submodules.
set -euo pipefail

LINK_TAG="Link-4.1"
ASIO_COMMIT="8806a6803cde7054c3049d3666d3ec36786568c5"

here="$(cd "$(dirname "$0")" && pwd)"
native="$(cd "$here/.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

echo "fetching $LINK_TAG and asio $ASIO_COMMIT"
curl -fsSL "https://github.com/Ableton/link/archive/refs/tags/$LINK_TAG.tar.gz" | tar xz -C "$work"
curl -fsSL "https://github.com/chriskohlhoff/asio/archive/$ASIO_COMMIT.tar.gz" | tar xz -C "$work"

link="$work/link-$LINK_TAG"
rmdir "$link/modules/asio-standalone"
mv "$work/asio-$ASIO_COMMIT" "$link/modules/asio-standalone"
cp -r "$here" "$work/recipe"

docker build --load -q -t jinglebox2-abletonlink "$here" >/dev/null

docker run --rm --user "$(id -u):$(id -g)" -v "$work:/w" -w /w/recipe jinglebox2-abletonlink bash -euc '
  set -o pipefail
  one() {
    local out=$1; shift
    cmake -S . -B "$out" -G Ninja -DCMAKE_BUILD_TYPE=Release -DLINK_DIR=/w/link-'"$LINK_TAG"' "$@" >/dev/null
    cmake --build "$out" | tail -1
  }
  one x64
  strip x64/libabl_link.so
  one arm64 -DCMAKE_TOOLCHAIN_FILE=/w/recipe/arm64.cmake
  aarch64-linux-gnu-strip arm64/libabl_link.so
  one win -DCMAKE_TOOLCHAIN_FILE=/w/recipe/mingw.cmake
  x86_64-w64-mingw32-strip win/abl_link.dll
'

cp "$work/recipe/x64/libabl_link.so" "$native/linux-x64/libabl_link.so"
cp "$work/recipe/arm64/libabl_link.so" "$native/linux-arm64/libabl_link.so"
cp "$work/recipe/win/abl_link.dll" "$native/win-x64/abl_link.dll"

for f in "$native/linux-x64/libabl_link.so" "$native/linux-arm64/libabl_link.so" "$native/win-x64/abl_link.dll"; do
  printf '%s  %s\n' "$(sha256sum "$f" | cut -c1-16)" "$(file -b "$f" | cut -c1-60)"
done
