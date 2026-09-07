#!/usr/bin/env bash
#
# A .NET publish for a Linux runtime leaves a native library under `runtimes/<rid>/native/`, and
# nothing looks for one there: the loader wants it beside the executable. So each one is carried
# up, and this is the one place that says which.
#
# It was three places, one per payload, each naming the files one by one: the tarball's step, the
# RPM's and the .deb's. `libbassmix.so` was added to the repository and to the csproj and to none
# of the three, so every Linux release since went out without it. Nothing said so, because each
# copy was wrapped in a test for the file being there and a file that is not there was passed
# over in silence. The Windows half of the same workflow has a step that refuses a payload with
# no `bassmix.dll` in it, which is the whole of why Windows kept working.
#
# What the absence costs is the application, not a feature. Summing what is played into one
# stream is BASSmix, and since the output bus stopped being a switch there is no second path: the
# mixer stream cannot be opened, and the first thing to ask for audio throws where nothing is
# holding a window yet.
#
# So the carrying and the checking are one act here. A library this program cannot run without is
# fatal and a library that costs a format is a warning, which is the line the Windows step
# already draws.
#
# Usage: linux-natives.sh <publish-output-dir> <rid>

set -euo pipefail

OUT="${1:?usage: linux-natives.sh <publish-output-dir> <rid>}"
RID="${2:?usage: linux-natives.sh <publish-output-dir> <rid>}"

NATIVE="$OUT/runtimes/$RID/native"

# Without these the program does not start. libbass is the audio library itself and libbassmix is
# what sums the pads, the tracker and the takes into the one stream everything leaves through.
REQUIRED=(libbass.so libbassmix.so)

# Without this an AAC stream will not decode and everything else is untouched.
OPTIONAL=(libbass_aac.so)

carry() {
  if [ -f "$NATIVE/$1" ]; then
    cp -a "$NATIVE/$1" "$OUT/$1"
    return 0
  fi
  [ -f "$OUT/$1" ]
}

missing=0

for lib in "${REQUIRED[@]}"; do
  if carry "$lib"; then
    echo "OK: $lib is beside the program"
  else
    echo "ERROR: $lib is in neither $NATIVE nor $OUT, and the application cannot start without it"
    missing=1
  fi
done

for lib in "${OPTIONAL[@]}"; do
  if carry "$lib"; then
    echo "OK: $lib is beside the program"
  else
    echo "WARNING: $lib is missing, so AAC streams will not play"
  fi
done

# .NET's tracepoint provider wants lttng at load time and is no use here, so a payload carrying
# it only buys a warning on every start on a machine without it.
rm -f "$OUT/libcoreclrtraceptprovider.so"

exit "$missing"
