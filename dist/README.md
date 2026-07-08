# Release artifacts

Pre-built files shipped with the macOS Compatibility Build:

| File | Description |
|------|-------------|
| `FM26PlayerExport.dll` | Patched export plugin (v5.1.0-macos) |
| `bepinex-macos-patch/BepInEx.Core.dll` | BepInEx core with tick pump support |
| `bepinex-macos-patch/BepInEx.Unity.IL2CPP.dll` | IL2CPP chainloader with `MainThreadTick` |

Install via `bash install_macos.sh` from the repo root (see README).

To refresh after a source build:

```bash
bash build_plugin.sh
cp plugin/bin/Release/net6.0/FM26PlayerExport.dll dist/FM26PlayerExport.dll
```
