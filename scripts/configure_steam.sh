#!/bin/bash
# Set FM26 Steam launch options to run_bepinex_arm64.sh in all userdata profiles.
#
#   GAME_DIR="/path/to/Football Manager 26" bash scripts/configure_steam.sh
#
# Opt out during install: FM26_SKIP_STEAM_LAUNCH=1 bash install_macos.sh
# Keep Steam running (not recommended): FM26_SKIP_STEAM_KILL=1 bash install_macos.sh
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
GAME="${1:-${FM26_GAME:-}}"
APP_ID="${FM26_STEAM_APPID:-3551340}"
STEAM_USERDATA="${FM26_STEAM_USERDATA:-$HOME/Library/Application Support/Steam/userdata}"

steam_running() {
  pgrep -if '/Steam\.app/' >/dev/null 2>&1 || pgrep -x Steam >/dev/null 2>&1
}

stop_steam() {
  if ! steam_running; then
    echo "   Steam not running"
    return 0
  fi

  echo "   quitting Steam (must be closed before editing localconfig.vdf)..."
  osascript -e 'tell application "Steam" to quit' 2>/dev/null || true

  for _ in 1 2 3 4 5 6; do
    sleep 1
    steam_running || break
  done

  if steam_running; then
    echo "   graceful quit timed out — force closing Steam processes"
    pkill -if '/Steam\.app/' 2>/dev/null || true
    pkill -x Steam 2>/dev/null || true
    pkill -if 'steam_osx' 2>/dev/null || true
    sleep 1
  fi

  if steam_running; then
    echo "WARNING: Steam still running — launch options may be overwritten on exit" >&2
    return 1
  fi

  echo "   Steam closed"
}

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

if [ "${FM26_SKIP_STEAM_KILL:-0}" != "1" ]; then
  stop_steam || true
else
  echo "   skipped Steam quit (FM26_SKIP_STEAM_KILL=1) — ensure Steam is closed manually"
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
  echo "   Steam launch options set — reopen Steam and launch FM26 from your library"
fi
