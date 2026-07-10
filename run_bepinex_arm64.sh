#!/bin/sh
# BepInEx launcher for FM26 running NATIVELY as arm64 (no Rosetta).
# Differences vs stock run_bepinex.sh:
#   - coreclr/corlib point to the arm64 .NET runtime (dotnet_arm64)
#   - the game is exec'd natively (NO `arch -x86_64`), so inline hooks are reliable
#
# Usage (from the game folder):
#   ./run_bepinex_arm64.sh
# or as a Steam launch option:
#   "/full/path/run_bepinex_arm64.sh" %command%

executable_name="fm.app"
enabled="1"
target_assembly="BepInEx/core/BepInEx.Unity.IL2CPP.dll"
boot_config_override=
ignore_disable_switch="0"
dll_search_path_override=""
debug_enable="0"
debug_address="127.0.0.1:10000"
debug_suspend="0"

# arm64 runtime folder assembled by setup_arm64.sh
coreclr_path="dotnet_arm64/libcoreclr"
corlib_dir="dotnet_arm64"

set -e

# Steam bootstrapper passthrough (same logic as stock script)
for a in "$@"; do
    if [ "$a" = "SteamLaunch" ]; then
        rotated=0; max=$#
        while [ $rotated -lt $max ]; do
            if [ "$1" != "${1#"${PWD%/}/"}" ]; then
                to_rotate=$(($# - rotated))
                set -- "$@" "$0"
                while [ $((to_rotate-=1)) -ge 0 ]; do
                    set -- "$@" "$1"; shift
                done
                exec "$@"
            else
                set -- "$@" "$1"; shift; rotated=$((rotated+1))
            fi
        done
        echo "Could not determine game executable launched by Steam" 1>&2
        exit 1
    fi
done

if [ -x "$1" ] ; then executable_name="$1"; shift; fi

a="/$0"; a=${a%/*}; a=${a#/}; a=${a:-.}; BASEDIR=$(cd "$a" || exit; pwd -P)

abs_path() {
    if [ "$1" = "${1#/}" ]; then set -- "${BASEDIR}/${1}"; fi
    echo "$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
}

real_executable_name="$(abs_path "$executable_name")"
case $real_executable_name in
    *.app/Contents/MacOS/*) executable_path="${executable_name}" ;;
    *)
        if [ "$real_executable_name" = "${real_executable_name%.app}" ]; then
            real_executable_name="${real_executable_name}.app"
        fi
        inner_executable_name=$(defaults read "${real_executable_name}/Contents/Info" CFBundleExecutable)
        executable_path="${real_executable_name}/Contents/MacOS/${inner_executable_name}"
    ;;
esac
lib_extension="dylib"

_readlink() {
    ab_path="$(abs_path "$1")"; link="$(readlink "${ab_path}")"
    case $link in /*);; *) link="$(dirname "$ab_path")/$link";; esac
    echo "$link"
}
resolve_executable_path () {
    e_path="$(abs_path "$1")"
    while [ -L "${e_path}" ]; do e_path=$(_readlink "${e_path}"); done
    echo "${e_path}"
}
executable_path=$(resolve_executable_path "${executable_path}")

# --- JIT-entitled shadow bundle -------------------------------------------
# The stock FM binary is hardened-runtime signed by SEGA WITHOUT
# com.apple.security.cs.allow-jit, and .NET CoreCLR requires JIT on Apple
# Silicon. With plain injection the game either dies with SIGTRAP in
# pthread_jit_write_protect_np during doorstop bootstrap, or dyld refuses the
# injection entirely and the game boots without BepInEx (no logs at all).
#
# We cannot re-sign the Steam bundle in place (App Management TCC blocks it,
# and Steam restores the original file on update/verify anyway). Instead we
# build a tiny "shadow" .app on APFS: symlinks to the real Contents plus a
# copy of the main executable, ad-hoc re-signed with the original
# entitlements + the JIT ones. Rebuilt automatically whenever Steam updates
# the game binary.
shadow_root="${FM26_BEP:-$HOME/fm26_bep}"
shadow_app="$shadow_root/fm.app"
shadow_bin="$shadow_app/Contents/MacOS/$(basename "$executable_path")"
real_contents="$(dirname "$(dirname "$executable_path")")"

# BepInEx derives its game paths from the *executable* location, so the shadow
# root must mirror the game root: Data, GameAssembly.dylib and BepInEx itself
# are symlinked next to fm.app. Refresh on every launch (cheap, fixes moves).
mkdir -p "$shadow_root"
ln -sfn "$real_contents/Resources/Data"               "$shadow_root/Data"
ln -sfn "$real_contents/Frameworks/GameAssembly.dylib" "$shadow_root/GameAssembly.dylib"
ln -sfn "$BASEDIR/BepInEx"                             "$shadow_root/BepInEx"

if [ ! -f "$shadow_bin" ] || [ "$executable_path" -nt "$shadow_bin" ]; then
    echo "[arm64-launcher] building JIT-entitled shadow bundle at $shadow_app"
    mkdir -p "$shadow_app/Contents/MacOS"
    ln -sfn "$real_contents/Frameworks" "$shadow_app/Contents/Frameworks"
    ln -sfn "$real_contents/PlugIns"    "$shadow_app/Contents/PlugIns"
    ln -sfn "$real_contents/Resources"  "$shadow_app/Contents/Resources"
    cp -f "$real_contents/Info.plist"   "$shadow_app/Contents/Info.plist"
    cp -f "$executable_path" "$shadow_bin"

    ents="$(mktemp /tmp/fm26_ents.XXXXXX)"
    codesign -d --entitlements "$ents" --xml "$executable_path" 2>/dev/null || true
    if [ ! -s "$ents" ]; then
        printf '<?xml version="1.0" encoding="UTF-8"?>\n<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">\n<plist version="1.0"><dict/></plist>\n' > "$ents"
    fi
    for k in com.apple.security.cs.allow-jit \
             com.apple.security.cs.allow-unsigned-executable-memory \
             com.apple.security.cs.disable-executable-page-protection \
             com.apple.security.cs.allow-dyld-environment-variables \
             com.apple.security.cs.disable-library-validation; do
        /usr/libexec/PlistBuddy -c "Add :$k bool true" "$ents" 2>/dev/null \
            || /usr/libexec/PlistBuddy -c "Set :$k true" "$ents"
    done
    if ! codesign -f -s - --entitlements "$ents" "$shadow_bin"; then
        echo "[arm64-launcher] ERROR: codesign of shadow binary failed" >&2
        rm -f "$ents"
        exit 1
    fi
    rm -f "$ents"
fi
executable_path="$shadow_bin"
# ---------------------------------------------------------------------------

target_assembly="$(abs_path "$target_assembly")"

export DOORSTOP_ENABLED="$enabled"
export DOORSTOP_TARGET_ASSEMBLY="$target_assembly"
export DOORSTOP_BOOT_CONFIG_OVERRIDE="$boot_config_override"
export DOORSTOP_IGNORE_DISABLED_ENV="$ignore_disable_switch"
export DOORSTOP_MONO_DLL_SEARCH_PATH_OVERRIDE="$dll_search_path_override"
export DOORSTOP_MONO_DEBUG_ENABLED="$debug_enable"
export DOORSTOP_MONO_DEBUG_ADDRESS="$debug_address"
export DOORSTOP_MONO_DEBUG_SUSPEND="$debug_suspend"
export DOORSTOP_CLR_RUNTIME_CORECLR_PATH="$BASEDIR/$coreclr_path.$lib_extension"
export DOORSTOP_CLR_CORLIB_DIR="$BASEDIR/$corlib_dir"

# Allow net6-targeted BepInEx assemblies to run on a newer arm64 runtime if needed.
export DOTNET_ROLL_FORWARD="LatestMajor"
export DOTNET_ROLL_FORWARD_TO_PRERELEASE="1"

doorstop_directory="${BASEDIR}/"
doorstop_name="libdoorstop.${lib_extension}"
doorstop_lib="${doorstop_directory}${doorstop_name}"
game_dyld_library_path="${BASEDIR}:${DOORSTOP_CLR_CORLIB_DIR}"

# Fail loudly instead of booting a vanilla game: if any of these is missing,
# injection silently does nothing ("game runs but no BepInEx / no logs").
err=0
if [ ! -f "$doorstop_lib" ]; then
    echo "[arm64-launcher] ERROR: $doorstop_lib not found." >&2
    echo "  Install stock BepInEx 6 IL2CPP into the game folder first." >&2
    err=1
fi
if [ ! -f "$DOORSTOP_CLR_RUNTIME_CORECLR_PATH" ]; then
    echo "[arm64-launcher] ERROR: arm64 .NET runtime not found at $DOORSTOP_CLR_RUNTIME_CORECLR_PATH" >&2
    echo "  Run setup_arm64.sh (or install_macos.sh) with FM26_GAME set to this game folder." >&2
    err=1
fi
if [ ! -f "$target_assembly" ]; then
    echo "[arm64-launcher] ERROR: BepInEx assembly not found at $target_assembly" >&2
    err=1
fi
[ "$err" = "1" ] && exit 1

# IMPORTANT: DYLD_* variables are intentionally NOT exported here. `/usr/bin/arch`
# is an Apple arm64e platform binary; with SIP relaxed, dyld would try to inject
# our x86_64/arm64 doorstop into `arch` itself and abort with
# "missing compatible architecture (have 'x86_64,arm64', need 'arm64e')".
# With SIP enabled dyld silently strips them for `arch` anyway. Either way the
# only reliable channel to the game process is `arch -e VAR=value`.

# NATIVE arm64 launch. We must force arm64 explicitly with `arch -arm64`:
#  - a universal binary can otherwise be run as x86_64 if ARCHPREFERENCE leaked
#    from a prior stock-script run in the same shell.
export ARCHPREFERENCE="arm64"

# CWD must be the game folder: Steam's DRM check exits the process silently
# (no crash report, log ends right after Unity's memory-setup dump) when the
# game is started from another directory, and the relative dotnet_arm64 paths
# above are resolved against CWD too.
cd "$BASEDIR" || exit 1

# Diagnostics: capture the game's own stdout/stderr so early crashes (before
# BepInEx can log) are visible. Written to APFS so it is always readable.
DIAG_LOG="/tmp/fm26_arm64_launch.log"
{
  echo "=== $(date '+%H:%M:%S') arm64 launcher ==="
  echo "executable_path=$executable_path"
  echo "doorstop_lib=$doorstop_lib ($(file -b "$doorstop_lib" 2>/dev/null | head -1))"
  echo "coreclr=$DOORSTOP_CLR_RUNTIME_CORECLR_PATH corlib=$DOORSTOP_CLR_CORLIB_DIR"
  echo "arch -arm64 present: $(command -v arch)"
} > "$DIAG_LOG"

echo "[arm64-launcher] launching under arch -arm64; game output -> $DIAG_LOG"
# All DYLD_* vars go through `arch -e` (see note above): they must reach the game
# process without being applied to /usr/bin/arch itself (an arm64e binary that
# our x86_64/arm64 dylibs cannot inject into).
exec arch -arm64 \
     -e DYLD_INSERT_LIBRARIES="${doorstop_lib}" \
     -e DYLD_LIBRARY_PATH="${game_dyld_library_path}:${DYLD_LIBRARY_PATH}" \
     "$executable_path" "$@" >> "$DIAG_LOG" 2>&1
