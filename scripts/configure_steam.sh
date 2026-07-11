#!/bin/bash
# Set FM26 Steam launch options to run_bepinex_arm64.sh in all userdata profiles.
#
#   GAME_DIR="/path/to/Football Manager 26" bash scripts/configure_steam.sh
#
# Opt out during install: FM26_SKIP_STEAM_LAUNCH=1 bash install_macos.sh
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
GAME="${1:-${FM26_GAME:-}}"
APP_ID="${FM26_STEAM_APPID:-3551340}"
STEAM_USERDATA="${FM26_STEAM_USERDATA:-$HOME/Library/Application Support/Steam/userdata}"

if [ -z "$GAME" ]; then
  echo "ERROR: set GAME_DIR or FM26_GAME" >&2
  exit 1
fi

if [ ! -x "$GAME/run_bepinex_arm64.sh" ]; then
  echo "ERROR: launcher not found or not executable: $GAME/run_bepinex_arm64.sh" >&2
  exit 1
fi

if [ ! -d "$STEAM_USERDATA" ]; then
  echo "WARNING: Steam userdata not found at $STEAM_USERDATA — skip Steam launch options" >&2
  exit 0
fi

PY="${FM26_PYTHON:-python3}"
if ! command -v "$PY" >/dev/null 2>&1; then
  echo "WARNING: python3 not found — cannot update Steam launch options" >&2
  exit 0
fi

updated=0
for cfg in "$STEAM_USERDATA"/*/config/localconfig.vdf; do
  [ -f "$cfg" ] || continue
  out="$("$PY" "$HERE/set_steam_launch_options.py" "$APP_ID" "$GAME" "$cfg" 2>&1 || true)"
  if [ -n "$out" ]; then
    echo "   $out"
    case "$out" in updated:*) updated=1 ;; esac
  fi
done

if [ "$updated" -eq 0 ]; then
  echo "   no Steam profiles updated (FM26 app block missing? set launch options manually)"
else
  echo "   Steam launch options set — quit Steam completely and relaunch before playing"
fi
