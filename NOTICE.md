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

The files in `dist/bepinex-macos-patch/` are modified builds of
[BepInEx 6 IL2CPP](https://github.com/BepInEx/BepInEx) (LGPL-2.1).

We add a **main-thread tick** (`IL2CPPChainloader.MainThreadTick`) so plugins
can run per-frame work on macOS arm64 without Unity `MonoBehaviour` injection.
See `docs/BEPINEX-PATCH.md`.

## Dobby

`libdobby.arm64.dylib` is from the [Dobby](https://github.com/jmpews/Dobby)
hooking library. Check its license before redistributing.

## Il2Cpp interop assemblies

Generated interop DLLs under `~/fm26_bep/interop/` are **not** shipped here.
They are produced locally on first BepInEx boot from your FM26 install and must
not be redistributed (game-derived).
