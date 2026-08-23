#!/usr/bin/env bash
#
# The presets are the only data the program ships beside itself, and a build that drops them
# still starts, still runs, and simply offers nothing to start a sound from: MachinePresets.Read
# treats a missing folder as a machine with no presets rather than an error. So nothing else
# would notice. This does.
#
# Usage: verify-presets.sh <publish-output-dir>

set -euo pipefail

OUT="${1:?usage: verify-presets.sh <publish-output-dir>}"

# A folder that is not there has none of whatever was asked for. Said this way rather than by
# letting find fail, because under pipefail a failing find takes the whole check down before it
# can say what was wrong, which is the one case worth a clear message.
count() {
  [ -d "$1" ] || { echo 0; return 0; }
  find "$1" -type f -name "$2" | wc -l | tr -d '[:space:]'
}

EXPECTED_JSON="$(count Presets '*.json')"
EXPECTED_WAV="$(count Presets '*.wav')"
ACTUAL_JSON="$(count "$OUT/Presets" '*.json')"
ACTUAL_WAV="$(count "$OUT/Presets" '*.wav')"

echo "presets: $ACTUAL_JSON/$EXPECTED_JSON json, $ACTUAL_WAV/$EXPECTED_WAV wav"

if [ "$EXPECTED_JSON" -eq 0 ]; then
  echo "ERROR: no presets in the source tree; the check itself is broken"
  exit 1
fi

if [ "$ACTUAL_JSON" -ne "$EXPECTED_JSON" ] || [ "$ACTUAL_WAV" -ne "$EXPECTED_WAV" ]; then
  echo "ERROR: presets missing from $OUT/Presets"
  find "$OUT/Presets" -type f 2>/dev/null || echo "  (the folder is not there at all)"
  exit 1
fi

# A machine with a folder but no readable preset in it is the same failure, one level down.
for machine in Presets/*/; do
  name="$(basename "$machine")"
  if [ "$(count "$OUT/Presets/$name" '*.json')" -ne "$(count "$machine" '*.json')" ]; then
    echo "ERROR: $name is missing presets"
    exit 1
  fi
done

echo "OK: presets present"
