#!/bin/bash
# FM26 Player Export — macOS Compatibility Build
# One-shot installer: BepInEx (bundled) + arm64 launcher + plugins.
#
#   FM26_GAME="/path/to/Football Manager 26" bash install_macos.sh
#
# Prerequisites: Football Manager 26 (Steam, macOS). Nothing else — BepInEx core,
# the doorstop injector, and all plugins are bundled. A pre-existing stock BepInEx
# install is fine too (we back up its core DLLs before replacing them).
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

if [ ! -d "$GAME/fm.app" ] && [ ! -f "$GAME/GameAssembly.dylib" ]; then
  echo "ERROR: $GAME does not look like an FM26 install (no fm.app / GameAssembly.dylib)" >&2
  exit 1
fi

if [ ! -d "$GAME/BepInEx" ]; then
  echo "No existing BepInEx found — fresh install (BepInEx is bundled)."
  mkdir -p "$GAME/BepInEx/core" "$GAME/BepInEx/plugins" "$GAME/BepInEx/config"
fi

for f in "$HERE/dist/FM26PlayerExport.dll" \
         "$HERE/dist/bepinex-core/BepInEx.Core.dll" \
         "$HERE/dist/bepinex-core/BepInEx.Unity.IL2CPP.dll"; do
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

echo "== 1/5 Arm64 launcher + doorstop + .NET runtime =="
bash "$HERE/setup_arm64.sh"

echo ""
echo "== 2/5 BepInEx core (full matched set) =="
# We install the COMPLETE BepInEx core we test against, not just the two
# patched DLLs: mixing our patched assemblies with a different BepInEx
# nightly fails at boot with e.g.
#   MissingMethodException: BepInEx.Paths.get_DisplayBepInExVersion()
CORE="$GAME/BepInEx/core"
BACKUP="$CORE/backup-stock"
if [ ! -d "$BACKUP" ] && ls "$CORE"/*.dll >/dev/null 2>&1; then
  mkdir -p "$BACKUP"
  find "$CORE" -maxdepth 1 -type f \( -name "*.dll" -o -name "*.dylib" \) \
    -exec cp {} "$BACKUP/" \;
  echo "   backed up stock core to $BACKUP"
fi
cp "$HERE/dist/bepinex-core/"* "$CORE/"
# exFAT drives grow AppleDouble ._ junk that BepInEx trips over — clean it.
find "$GAME/BepInEx" -name "._*" -delete 2>/dev/null || true
echo "   installed $(ls "$HERE/dist/bepinex-core" | wc -l | tr -d ' ') core files"

echo ""
echo "== 3/5 Export plugin =="
PLUGIN_DEST="$GAME/BepInEx/plugins/FM26PlayerExport"
mkdir -p "$PLUGIN_DEST"
cp "$HERE/dist/FM26PlayerExport.dll" "$PLUGIN_DEST/FM26PlayerExport.dll"
echo "   installed $PLUGIN_DEST/FM26PlayerExport.dll"

echo ""
echo "== 4/5 Display Fix plugin (16:10 + ultrawide menus) =="
if [ -f "$HERE/dist/FM26DisplayFix.dll" ]; then
  DISPLAY_DEST="$GAME/BepInEx/plugins/FM26DisplayFix"
  mkdir -p "$DISPLAY_DEST"
  cp "$HERE/dist/FM26DisplayFix.dll" "$DISPLAY_DEST/FM26DisplayFix.dll"
  echo "   installed $DISPLAY_DEST/FM26DisplayFix.dll"
else
  echo "   skipped (run build_displayfix.sh first)"
fi

echo ""
echo "== 5/5 TFP view presets (optional) =="
VIEWS_DEST="$HOME/Library/Application Support/Sports Interactive/Football Manager 26/views"
if [ -d "$HERE/views" ]; then
  mkdir -p "$VIEWS_DEST"
  for f in tfp_basic_stats.fmf tfp_fm_squad_v1.fmf; do
    if [ -f "$HERE/views/$f" ]; then
      cp "$HERE/views/$f" "$VIEWS_DEST/$f"
      echo "   installed $VIEWS_DEST/$f"
    fi
  done
else
  echo "   skipped (no views/ folder)"
fi

echo ""
echo "== Done =="
echo ""
echo "Launch FM26 through the arm64 wrapper (required on Apple Silicon):"
echo "  cd \"$GAME\" && ./run_bepinex_arm64.sh"
echo ""
echo "Steam → FM26 → Properties → Launch Options:"
echo "  \"$GAME/run_bepinex_arm64.sh\" %command%"
echo ""
echo "In game — load a view (TFP presets or your own), select rows, press F9:"
echo "  Squad         → tfp_fm_squad_v1  (or custom view)"
echo "  Player Search → tfp_basic_stats  (or custom view)"
echo ""
echo "CSV output:"
echo "  ~/Sports Interactive/Football Manager 26/FM26PlayerExport by vinteset/Exports CSV/"
echo "  (HTML in .../Exports HTML/)"
echo ""
echo "Tune stability in BepInEx/config/com.koda.fm26.playerexport.cfg"
echo "  ScrollStepDelayFrames = 18   # raise to 24–30 for 500+ row lists"
echo ""
echo "Support: https://buymeacoffee.com/tfpdev"
