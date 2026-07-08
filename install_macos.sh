#!/bin/bash
# FM26 Player Export — macOS Compatibility Build
# One-shot installer: arm64 launcher + BepInEx patch + export plugin.
#
#   FM26_GAME="/path/to/Football Manager 26" bash install_macos.sh
#
# Prerequisites (manual, one time):
#   1. Football Manager 26 (Steam, macOS)
#   2. BepInEx 6 IL2CPP installed in the game folder (standard FM modding setup)
#   3. Launch the game once through BepInEx so interop assemblies are generated
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
GAME="${FM26_GAME:-}"

if [ -z "$GAME" ]; then
  echo "Set your FM26 install path, for example:" >&2
  echo '  FM26_GAME="$HOME/Library/Application Support/Steam/steamapps/common/Football Manager 26" bash install_macos.sh' >&2
  echo "" >&2
  echo "Or export FM26_GAME before running." >&2
  exit 1
fi

if [ ! -d "$GAME/BepInEx" ]; then
  echo "ERROR: BepInEx not found at $GAME/BepInEx" >&2
  echo "Install stock BepInEx 6 IL2CPP for FM26 first, then launch the game once." >&2
  exit 1
fi

for f in "$HERE/dist/FM26PlayerExport.dll" \
         "$HERE/dist/bepinex-macos-patch/BepInEx.Core.dll" \
         "$HERE/dist/bepinex-macos-patch/BepInEx.Unity.IL2CPP.dll"; do
  if [ ! -f "$f" ]; then
    echo "ERROR: missing release file: $f" >&2
    echo "Run build_plugin.sh or download a GitHub release bundle." >&2
    exit 1
  fi
done

export FM26_GAME="$GAME"
echo "== FM26 Player Export — macOS Compatibility Build =="
echo "Game: $GAME"
echo ""

echo "== 1/3 Arm64 launcher + .NET runtime =="
bash "$HERE/setup_arm64.sh"

echo ""
echo "== 2/3 BepInEx macOS patch =="
CORE="$GAME/BepInEx/core"
BACKUP="$CORE/backup-stock"
mkdir -p "$BACKUP"
for dll in BepInEx.Core.dll BepInEx.Unity.IL2CPP.dll; do
  if [ -f "$CORE/$dll" ] && [ ! -f "$BACKUP/$dll" ]; then
    cp "$CORE/$dll" "$BACKUP/$dll"
    echo "   backed up stock $dll"
  fi
  cp "$HERE/dist/bepinex-macos-patch/$dll" "$CORE/$dll"
  echo "   installed patched $dll"
done

echo ""
echo "== 3/3 Export plugin =="
PLUGIN_DEST="$GAME/BepInEx/plugins/FM26PlayerExport"
mkdir -p "$PLUGIN_DEST"
cp "$HERE/dist/FM26PlayerExport.dll" "$PLUGIN_DEST/FM26PlayerExport.dll"
echo "   installed $PLUGIN_DEST/FM26PlayerExport.dll"

echo ""
echo "== Done =="
echo ""
echo "Launch FM26 through the arm64 wrapper (required on Apple Silicon):"
echo "  cd \"$GAME\" && ./run_bepinex_arm64.sh"
echo ""
echo "Steam → FM26 → Properties → Launch Options:"
echo "  \"$GAME/run_bepinex_arm64.sh\" %command%"
echo ""
echo "In game: open Squad or Player Search, select rows, press F9."
echo "CSV output:"
echo "  ~/Documents/Sports Interactive/Football Manager 26/FM26PlayerExport by vinteset/"
echo ""
echo "Tune stability in BepInEx/config/com.koda.fm26.playerexport.cfg"
echo "  ScrollStepDelayFrames = 18   # raise to 24–30 for 500+ row lists"
