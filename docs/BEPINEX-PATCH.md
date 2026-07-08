# BepInEx macOS patch

Stock BepInEx 6 IL2CPP on macOS Apple Silicon fails in two ways relevant to
player export:

1. **Rosetta path** — inline hooks on `il2cpp_runtime_invoke` never fire after
   Rosetta caches translated code, so plugins load on a timer thread instead of
   the Unity main thread.
2. **arm64 path** — `Il2CppInterop.ClassInjector` cannot register new
   `MonoBehaviour` types, so plugins that drive export from `Update()` never
   tick.

## What we changed

We patch two assemblies (shipped in `dist/bepinex-macos-patch/`):

| File | Change |
|------|--------|
| `BepInEx.Unity.IL2CPP.dll` | `IL2CPPChainloader.MainThreadTick` event; `PumpMainThreadTick()` called from the `il2cpp_runtime_invoke` detour on the main thread |
| `BepInEx.Core.dll` | Supporting plumbing for the tick pump |

The FM26 export plugin subscribes to `MainThreadTick` via `ExportDriver` instead
of injecting `ExportBehaviour : MonoBehaviour`.

## Install

`install_macos.sh` backs up your existing core DLLs to
`BepInEx/core/backup-stock/` and copies the patched files in.

To restore stock BepInEx:

```bash
cp BepInEx/core/backup-stock/*.dll BepInEx/core/
```

## Upstream

This patch should eventually be contributed to BepInEx or documented as an
official macOS IL2CPP workaround. Until then, treat `dist/bepinex-macos-patch/`
as part of this compatibility build release.
