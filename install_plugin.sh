#!/bin/bash
# Copy built plugin DLL into BepInEx plugins folder.
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
GAME="${FM26_GAME:-/Volumes/T7/SteamLibrary/steamapps/common/Football Manager 26}"
BEP="${FM26_BEP:-$HOME/fm26_bep}"
DLL="$HERE/plugin/bin/Release/net6.0/FM26PlayerExport.dll"
DEST="${FM26_PLUGIN_DIR:-$BEP/plugins/FM26PlayerExport}"

if [ ! -f "$DLL" ]; then
  echo "ERROR: $DLL not found — run build_plugin.sh first." >&2
  exit 1
fi

mkdir -p "$DEST"
cp "$DLL" "$DEST/FM26PlayerExport.dll"

# Mirror into game folder if it differs from fm26_bep (symlinked installs).
GAME_DEST="$GAME/BepInEx/plugins/FM26PlayerExport"
if [ -d "$GAME/BepInEx" ] && [ "$(cd "$GAME_DEST" 2>/dev/null && pwd -P)" != "$(cd "$DEST" && pwd -P)" ]; then
  mkdir -p "$GAME_DEST"
  cp "$DLL" "$GAME_DEST/FM26PlayerExport.dll"
  echo "Installed to $GAME_DEST/FM26PlayerExport.dll"
fi

echo "Installed to $DEST/FM26PlayerExport.dll"
