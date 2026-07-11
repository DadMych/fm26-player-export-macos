#!/bin/bash
# One-time setup for running FM26 + BepInEx NATIVELY as arm64 (no Rosetta).
# Run this in a normal Terminal (NOT inside the sandboxed agent).
#
#   bash setup_arm64.sh
#
# Requires FM26_GAME pointing at your install folder.
# It will:
#   1. Assemble an arm64 .NET runtime on APFS (avoids exFAT AppleDouble issues).
#   2. Install a universal (x86_64+arm64) libdobby.dylib so both launch modes work.
#   3. Symlink the arm64 runtime into the game folder.
#   4. Copy the arm64 launch script into the game folder.
set -e

GAME="${FM26_GAME:-}"
if [ -z "$GAME" ]; then
  echo "Set FM26_GAME to your Football Manager 26 install folder." >&2
  echo '  export FM26_GAME="$HOME/Library/Application Support/Steam/steamapps/common/Football Manager 26"' >&2
  exit 1
fi
APFS="${FM26_BEP:-$HOME/fm26_bep}"
DST="$APFS/dotnet_arm64"
HERE="$(cd "$(dirname "$0")" && pwd)"

echo "== 1. Assemble arm64 .NET runtime at $DST =="
mkdir -p "$DST"
rm -f "$DST"/*.dll "$DST"/*.dylib 2>/dev/null || true

# Preferred: exact .NET 6.0.7 arm64 runtime (matches BepInEx target). Falls back
# to the locally installed arm64 .NET runtime (roll-forward) if download fails.
TARBALL_URL="https://builds.dotnet.microsoft.com/dotnet/Runtime/6.0.7/dotnet-runtime-6.0.7-osx-arm64.tar.gz"
TMPRT="$(mktemp -d)"
if curl -fsSL "$TARBALL_URL" -o "$TMPRT/rt.tgz" 2>/dev/null && tar -xzf "$TMPRT/rt.tgz" -C "$TMPRT" 2>/dev/null; then
    SRC="$(dirname "$(find "$TMPRT/shared" -name libcoreclr.dylib | head -1)")"
    echo "   using downloaded .NET 6.0.7 arm64 runtime"
else
    SRC="$(ls -d /opt/homebrew/Cellar/dotnet/*/libexec/shared/Microsoft.NETCore.App/* 2>/dev/null | sort -V | tail -1)"
    echo "   download unavailable; falling back to local arm64 runtime: $SRC"
    echo "   (net6 BepInEx assemblies will roll forward onto this runtime)"
fi
[ -z "$SRC" ] && { echo "ERROR: no arm64 .NET runtime found. Install one: brew install dotnet"; exit 1; }

cp "$SRC"/*.dll "$SRC"/*.dylib "$DST"/
# BepInEx also ships these 7 managed helpers in its x86 dotnet folder; carry them
# over from the stock install if present, otherwise use our bundled copies.
for d in Microsoft.Bcl.AsyncInterfaces.dll \
         Microsoft.Extensions.DependencyInjection.Abstractions.dll \
         Microsoft.Extensions.DependencyInjection.dll \
         Microsoft.Extensions.Logging.Abstractions.dll \
         Microsoft.Extensions.Logging.dll \
         Microsoft.Extensions.Options.dll \
         Microsoft.Extensions.Primitives.dll; do
    if [ -f "$GAME/dotnet/$d" ]; then
        cp "$GAME/dotnet/$d" "$DST/$d"
    elif [ -f "$HERE/dist/dotnet-extras/$d" ]; then
        cp "$HERE/dist/dotnet-extras/$d" "$DST/$d"
    else
        echo "WARNING: helper DLL $d not found (stock dotnet/ or dist/dotnet-extras/)" >&2
    fi
done
# doorstop expects the coreclr basename without extension = "libcoreclr"
[ -f "$DST/libcoreclr.dylib" ] || { echo "ERROR: libcoreclr.dylib missing in $DST"; exit 1; }
echo "   assembled: $(ls "$DST"/*.dll | wc -l | tr -d ' ') dll, $(ls "$DST"/*.dylib | wc -l | tr -d ' ') dylib"

echo "== 2. Install doorstop injector =="
if [ ! -f "$GAME/libdoorstop.dylib" ]; then
    cp "$HERE/libdoorstop.dylib" "$GAME/libdoorstop.dylib"
    echo "   installed bundled libdoorstop.dylib (universal)"
else
    echo "   keeping existing libdoorstop.dylib"
fi

echo "== 3. Install universal libdobby.dylib =="
CORE="$GAME/BepInEx/core"
mkdir -p "$CORE"
if [ ! -f "$CORE/libdobby.x86_64.dylib.bak" ] && [ -f "$CORE/libdobby.dylib" ]; then
    cp "$CORE/libdobby.dylib" "$CORE/libdobby.x86_64.dylib.bak"
fi
if [ -f "$CORE/libdobby.x86_64.dylib.bak" ]; then
    lipo -create "$CORE/libdobby.x86_64.dylib.bak" "$HERE/libdobby.arm64.dylib" \
        -output "$CORE/libdobby.dylib" && echo "   libdobby.dylib is now universal:"
else
    cp "$HERE/libdobby.arm64.dylib" "$CORE/libdobby.dylib" && echo "   installed arm64 libdobby.dylib:"
fi
file "$CORE/libdobby.dylib"

echo "== 4. Symlink arm64 runtime into game folder =="
ln -sfn "$DST" "$GAME/dotnet_arm64"
ls -ld "$GAME/dotnet_arm64"

echo "== 5. Install arm64 launch script =="
cp "$HERE/run_bepinex_arm64.sh" "$GAME/run_bepinex_arm64.sh"
chmod +x "$GAME/run_bepinex_arm64.sh"
echo "   $GAME/run_bepinex_arm64.sh"

echo ""
echo "DONE. Launch natively-arm64 with:"
echo "  cd \"$GAME\" && ./run_bepinex_arm64.sh"
echo ""
echo "Or set Steam launch options to:"
echo "  \"$GAME/run_bepinex_arm64.sh\" %command%"
