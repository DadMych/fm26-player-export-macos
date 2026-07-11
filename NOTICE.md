# Third-party notices

## FM26 Player Export (original)

This macOS compatibility build is derived from **FM26 Player Export v5.1** by
**vinteset** (community BepInEx plugin for Football Manager 26).

- Original distribution: [FM Scout](https://www.fmscout.com/) / sortitoutsi
- Our changes: Apple Silicon native export, scroll stability, retry logic,
  configurable capture delay — see `README.md`

Football Manager is a trademark of Sports Interactive / SEGA. This project is
not affiliated with or endorsed by Sports Interactive or SEGA.

## BepInEx

The files in `dist/bepinex-core/` are the [BepInEx 6 IL2CPP](https://github.com/BepInEx/BepInEx)
core (LGPL-2.1) and its bundled dependencies (Harmony, MonoMod, Mono.Cecil,
Il2CppInterop, Cpp2IL, Iced, AsmResolver, Dobby, funchook, and others — each
under its own license), with these assemblies modified by us:

- `BepInEx.Core.dll`, `BepInEx.Unity.IL2CPP.dll` — we add a **main-thread
  tick** (`IL2CPPChainloader.MainThreadTick`) so plugins can run per-frame
  work on macOS arm64 without Unity `MonoBehaviour` injection.
  See `docs/BEPINEX-PATCH.md`.
- `Il2CppInterop.Runtime.dll`, `Il2CppInterop.Common.dll`,
  `Il2CppInterop.HarmonySupport.dll` — built from
  [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop) 1.5.1
  (commit `6d9007c` + PR #272) with our **arm64 injection support**:
  native A64 xref scanning, shim-safe hooking, and graceful degradation
  for hooks that cannot exist on arm64. Full source diff:
  `docs/il2cppinterop-arm64.patch` (Il2CppInterop is LGPL-2.1).

## UnityDoorstop

`libdoorstop.dylib` (universal x86_64+arm64) is from
[NeighTools/UnityDoorstop](https://github.com/NeighTools/UnityDoorstop)
(LGPL-3.0), as distributed with the BepInEx 6 IL2CPP pack. It is the native
injector that bootstraps BepInEx into the Unity process.

## Microsoft .NET helper assemblies

The files in `dist/dotnet-extras/` (`Microsoft.Bcl.AsyncInterfaces.dll`,
`Microsoft.Extensions.*.dll`) are unmodified Microsoft .NET libraries (MIT
license), as distributed with the BepInEx 6 IL2CPP pack.

## Dobby

`libdobby.arm64.dylib` is from the [Dobby](https://github.com/jmpews/Dobby)
hooking library. Check its license before redistributing.

## FM26 Display Fix

The bundled **FM26 Display Fix** plugin extends ideas from
[LionelFW/fm26ultrawidefix](https://github.com/LionelFW/fm26ultrawidefix)
(ultrawide menu scaling) with additional **16:10 / tall-aspect** support for
MacBook displays. Source: `plugin_displayfix/`.

## Il2Cpp interop assemblies

Generated interop DLLs under `~/fm26_bep/interop/` are **not** shipped here.
They are produced locally on first BepInEx boot from your FM26 install and must
not be redistributed (game-derived).
