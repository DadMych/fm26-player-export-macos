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

target_assembly="$(abs_path "$target_assembly")"

export DOORSTOP_ENABLED="$enabled"
export DOORSTOP_TARGET_ASSEMBLY="$target_assembly"
export DOORSTOP_BOOT_CONFIG_OVERRIDE="$boot_config_override"
export DOORSTOP_IGNORE_DISABLED_ENV="$ignore_disable_switch"
export DOORSTOP_MONO_DLL_SEARCH_PATH_OVERRIDE="$dll_search_path_override"
export DOORSTOP_MONO_DEBUG_ENABLED="$debug_enable"
export DOORSTOP_MONO_DEBUG_ADDRESS="$debug_address"
export DOORSTOP_MONO_DEBUG_SUSPEND="$debug_suspend"
export DOORSTOP_CLR_RUNTIME_CORECLR_PATH="$coreclr_path.$lib_extension"
export DOORSTOP_CLR_CORLIB_DIR="$corlib_dir"

# Allow net6-targeted BepInEx assemblies to run on a newer arm64 runtime if needed.
export DOTNET_ROLL_FORWARD="LatestMajor"
export DOTNET_ROLL_FORWARD_TO_PRERELEASE="1"

doorstop_directory="${BASEDIR}/"
doorstop_name="libdoorstop.${lib_extension}"

export DYLD_LIBRARY_PATH="${doorstop_directory}:${corlib_dir}:${DYLD_LIBRARY_PATH}"
if [ -z "$DYLD_INSERT_LIBRARIES" ]; then
    export DYLD_INSERT_LIBRARIES="${doorstop_name}"
else
    export DYLD_INSERT_LIBRARIES="${doorstop_name}:${DYLD_INSERT_LIBRARIES}"
fi

# NATIVE arm64 launch. We must force arm64 explicitly with `arch -arm64`:
#  - a universal binary can otherwise be run as x86_64 if ARCHPREFERENCE leaked
#    from a prior stock-script run in the same shell.
#  - `arch` strips DYLD_* env vars, so DYLD_INSERT_LIBRARIES must be re-passed via -e.
export ARCHPREFERENCE="arm64"

# Diagnostics: capture the game's own stdout/stderr so early crashes (before
# BepInEx can log) are visible. Written to APFS so it is always readable.
DIAG_LOG="/tmp/fm26_arm64_launch.log"
{
  echo "=== $(date '+%H:%M:%S') arm64 launcher ==="
  echo "executable_path=$executable_path"
  echo "DYLD_INSERT_LIBRARIES=$DYLD_INSERT_LIBRARIES"
  echo "coreclr=$DOORSTOP_CLR_RUNTIME_CORECLR_PATH corlib=$DOORSTOP_CLR_CORLIB_DIR"
  echo "arch -arm64 present: $(command -v arch)"
} > "$DIAG_LOG"

echo "[arm64-launcher] launching under arch -arm64; game output -> $DIAG_LOG"
exec arch -arm64 -e DYLD_INSERT_LIBRARIES="${DYLD_INSERT_LIBRARIES}" \
     "$executable_path" "$@" >> "$DIAG_LOG" 2>&1
