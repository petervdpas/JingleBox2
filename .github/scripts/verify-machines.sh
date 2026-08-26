#!/usr/bin/env bash
#
# The machines are what the program ships beside itself, and a build that drops them starts to
# an empty rack: a machine is a project on disc now, so no folder means no panels, no presets
# and nothing to make a sound with. The app would not complain, since a machines folder that is
# not there reads as an installation with none. So nothing else would notice. This does.
#
# Usage: verify-machines.sh <publish-output-dir>

set -euo pipefail

OUT="${1:?usage: verify-machines.sh <publish-output-dir>}"

# A folder that is not there has none of whatever was asked for. Said this way rather than by
# letting find fail, because under pipefail a failing find takes the whole check down before it
# can say what was wrong, which is the one case worth a clear message.
count() {
  [ -d "$1" ] || { echo 0; return 0; }
  find "$1" -type f -name "$2" | wc -l | tr -d '[:space:]'
}

EXPECTED_JSON="$(count machines '*.json')"
EXPECTED_WAV="$(count machines '*.wav')"
ACTUAL_JSON="$(count "$OUT/machines" '*.json')"
ACTUAL_WAV="$(count "$OUT/machines" '*.wav')"

echo "machines: $ACTUAL_JSON/$EXPECTED_JSON json, $ACTUAL_WAV/$EXPECTED_WAV wav"

if [ "$EXPECTED_JSON" -eq 0 ]; then
  echo "ERROR: no machines in the source tree; the check itself is broken"
  exit 1
fi

if [ "$ACTUAL_JSON" -ne "$EXPECTED_JSON" ] || [ "$ACTUAL_WAV" -ne "$EXPECTED_WAV" ]; then
  echo "ERROR: machines missing from $OUT/machines"
  find "$OUT/machines" -type f 2>/dev/null || echo "  (the folder is not there at all)"
  exit 1
fi

# A machine whose folder arrived without its machine.json is the same failure one level down,
# and so is one that lost the presets it starts you from.
for machine in machines/*/; do
  name="$(basename "$machine")"

  if [ ! -f "$OUT/machines/$name/machine.json" ]; then
    echo "ERROR: $name arrived without its machine.json"
    exit 1
  fi

  if [ "$(count "$OUT/machines/$name/presets" '*.json')" -ne "$(count "$machine/presets" '*.json')" ]; then
    echo "ERROR: $name is missing presets"
    exit 1
  fi
done

echo "OK: machines present"
