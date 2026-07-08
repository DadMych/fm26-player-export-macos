#!/bin/bash
# Build FM26PlayerExport.dll (macOS arm64 fork).
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
GAME="${FM26_GAME:-$HOME/Library/Application Support/Steam/steamapps/common/Football Manager 26}"
BEP="${FM26_BEP:-$HOME/fm26_bep}"
CORE="${FM26_CORE_DIR:-$GAME/BepInEx/core}"
INTEROP="${FM26_INTEROP_DIR:-$BEP/interop}"

for d in "$CORE" "$INTEROP"; do
  if [ ! -d "$d" ]; then
    echo "ERROR: missing directory: $d" >&2
    echo "Set FM26_GAME / FM26_CORE_DIR / FM26_INTEROP_DIR and ensure BepInEx has generated interop once." >&2
    exit 1
  fi
done

echo "Core:   $CORE"
echo "Interop: $INTEROP"
dotnet build "$HERE/plugin/FM26PlayerExport.csproj" -c Release \
  -p:CoreDir="$CORE" \
  -p:InteropDir="$INTEROP"

echo ""
echo "Built: $HERE/plugin/bin/Release/net6.0/FM26PlayerExport.dll"
