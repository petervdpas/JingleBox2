#!/usr/bin/env bash
#
# Whether the BASS libraries in native/ are still what un4seen ships.
#
# Compared by hash and never by size. Both Linux libbass_aac.so builds have at least once been
# byte-identical in size to the ones they replaced and a completely different build, so a size
# check reports "current" for a library that is a release behind.
#
# An archive whose name carries its version, which is all of them, moves when the version moves:
# bassasio14.zip becomes bassasio15.zip. A download that 404s is therefore not a failure to
# check, it is the loudest possible answer, and it is reported as one.
#
# Run it anywhere: it only reads native/ and writes to a temporary folder.

set -uo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

base="https://www.un4seen.com/files"

# shipped file | archive under files/ | path inside the archive
shipped=(
  "native/win-x64/bass.dll|bass24.zip|x64/bass.dll"
  "native/win-x64/bass_aac.dll|z/2/bass_aac24.zip|x64/bass_aac.dll"
  "native/win-x64/basswasapi.dll|basswasapi24.zip|x64/basswasapi.dll"
  "native/win-x64/bassasio.dll|bassasio14.zip|x64/bassasio.dll"
  "native/win-x64/bassmix.dll|bassmix24.zip|x64/bassmix.dll"
  "native/linux-x64/libbass.so|bass24-linux.zip|libs/x86_64/libbass.so"
  "native/linux-arm64/libbass.so|bass24-linux.zip|libs/aarch64/libbass.so"
  "native/linux-x64/libbass_aac.so|z/2/bass_aac24-linux.zip|libs/x86_64/libbass_aac.so"
  "native/linux-arm64/libbass_aac.so|z/2/bass_aac24-linux.zip|libs/aarch64/libbass_aac.so"
  "native/linux-x64/libbassmix.so|bassmix24-linux.zip|libs/x86_64/libbassmix.so"
  "native/linux-arm64/libbassmix.so|bassmix24-linux.zip|libs/aarch64/libbassmix.so"
)

behind=0
unreachable=0

for row in "${shipped[@]}"; do
  IFS='|' read -r ours archive inside <<< "$row"

  name="$(basename "$archive")"

  if [ ! -f "$root/$ours" ]; then
    echo "MISSING   $ours is not in this checkout"
    behind=$((behind + 1))
    continue
  fi

  if [ ! -f "$work/$name" ]; then
    code="$(curl -sS -L --max-time 120 -o "$work/$name" -w '%{http_code}' "$base/$archive" || echo 000)"

    if [ "$code" != "200" ]; then
      echo "MOVED     $archive answered $code, so its version has probably moved on"
      unreachable=$((unreachable + 1))
      rm -f "$work/$name"
      continue
    fi
  fi

  if ! unzip -o -j -q "$work/$name" "$inside" -d "$work/out" 2>/dev/null; then
    echo "MOVED     $inside is no longer inside $name"
    unreachable=$((unreachable + 1))
    continue
  fi

  theirs="$work/out/$(basename "$inside")"

  mine="$(sha256sum "$root/$ours" | cut -c1-16)"
  yours="$(sha256sum "$theirs" | cut -c1-16)"

  if [ "$mine" = "$yours" ]; then
    echo "current   $ours"
  else
    echo "BEHIND    $ours is $mine, un4seen ships $yours"
    behind=$((behind + 1))
  fi
done

echo

if [ "$unreachable" -gt 0 ]; then
  echo "$unreachable archive(s) could not be read. Look at https://www.un4seen.com/ for the new names."
fi

if [ "$behind" -gt 0 ]; then
  echo "$behind librarie(s) are behind. Download, drop into native/, and listen before trusting it."
fi

[ "$behind" -eq 0 ] && [ "$unreachable" -eq 0 ]
