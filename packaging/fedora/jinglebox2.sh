#!/usr/bin/bash
set -euo pipefail

APPDIR="/opt/JingleBox2"
export LD_LIBRARY_PATH="$APPDIR:${LD_LIBRARY_PATH:-}"

exec "$APPDIR/JingleBox2" "$@"
