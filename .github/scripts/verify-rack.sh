#!/usr/bin/env bash
#
# The rack is what the program ships beside itself, and a build that drops it starts to an empty
# one: a machine and an effect are each a project on disc, so no folder means no panels, no
# presets and nothing to make a sound with. The app would not complain, since a rack folder that
# is not there reads as an installation with none. So nothing else would notice. This does.
#
# It counted `machines/` at the top of the tree, which is where all of this lived until the two
# worlds were split. Both halves are under `rack/` now, `rack/machines` and `rack/effects`, and
# the check went on counting a folder nobody had moved anything into: nought against nought,
# which the guard below reads as the check itself being broken and says so. That guard is the
# only reason the rename did not ship a release with an empty rack in it.
#
# Usage: verify-machines.sh <publish-output-dir>

set -euo pipefail

OUT="${1:?usage: verify-machines.sh <publish-output-dir>}"

# Where the rack lives in the source tree, and under the same name in the output, since the
# csproj copies the folder rather than naming its contents.
RACK="rack"

# A folder that is not there has none of whatever was asked for. Said this way rather than by
# letting find fail, because under pipefail a failing find takes the whole check down before it
# can say what was wrong, which is the one case worth a clear message.
count() {
  [ -d "$1" ] || { echo 0; return 0; }
  find "$1" -type f -name "$2" | wc -l | tr -d '[:space:]'
}

EXPECTED_JSON="$(count "$RACK" '*.json')"
EXPECTED_WAV="$(count "$RACK" '*.wav')"
ACTUAL_JSON="$(count "$OUT/$RACK" '*.json')"
ACTUAL_WAV="$(count "$OUT/$RACK" '*.wav')"

echo "rack: $ACTUAL_JSON/$EXPECTED_JSON json, $ACTUAL_WAV/$EXPECTED_WAV wav"

if [ "$EXPECTED_JSON" -eq 0 ]; then
  echo "ERROR: nothing on the rack in the source tree; the check itself is broken"
  exit 1
fi

if [ "$ACTUAL_JSON" -ne "$EXPECTED_JSON" ] || [ "$ACTUAL_WAV" -ne "$EXPECTED_WAV" ]; then
  echo "ERROR: the rack is missing from $OUT/$RACK"
  find "$OUT/$RACK" -type f 2>/dev/null || echo "  (the folder is not there at all)"
  exit 1
fi

# A box whose folder arrived without its manifest is the same failure one level down, and so is
# one that lost the presets it starts you from. The two worlds differ in one word: a machine is
# described by machine.json and an effect by effect.json, deliberately, since a folder is one
# thing or the other and a reader that had to open the file to find out which can be wrong.
check() {
  local world="$1" manifest="$2" box name

  [ -d "$RACK/$world" ] || return 0

  for box in "$RACK/$world"/*/; do
    [ -d "$box" ] || continue

    name="$(basename "$box")"

    if [ ! -f "$OUT/$RACK/$world/$name/$manifest" ]; then
      echo "ERROR: $name arrived without its $manifest"
      exit 1
    fi

    if [ "$(count "$OUT/$RACK/$world/$name/presets" '*.json')" -ne "$(count "$box/presets" '*.json')" ]; then
      echo "ERROR: $name is missing presets"
      exit 1
    fi

    # And its own page, which is the one file here that is neither json nor wav and so is
    # counted by nothing above. A device whose help did not ship opens a window saying it has
    # none, which reads as the author having written nothing rather than as a payload with a
    # hole in it. Only where there is one to ship: a device is allowed to carry no page.
    if [ -f "$box/help.md" ] && [ ! -f "$OUT/$RACK/$world/$name/help.md" ]; then
      echo "ERROR: $name arrived without its help.md"
      exit 1
    fi
  done
}

check machines machine.json
check effects effect.json

echo "OK: the rack is present"
