# FM26 Player Export — macOS Compatibility Build

A community fork of [**FM26 Player Export by vinteset**](https://www.fmscout.com/) (v5.1) that makes **large player-list exports work on macOS Apple Silicon** — native arm64, no Rosetta crashes, stable scrolling through 500–10,000 rows.

Published at **[github.com/DadMych/fm26-player-export-macos](https://github.com/DadMych/fm26-player-export-macos)**.

Pair with **[TFP FM](https://github.com/DadMych/tfp_fm)** scouting analytics *(currently in development)* — upload the exported CSV to get archetypes, squad analysis, and transfer advice. Support: **[buymeacoffee.com/tfpdev](https://buymeacoffee.com/tfpdev)**.

---

## What this fixes

| Issue on stock macOS build | Our fix |
|----------------------------|---------|
| BepInEx hooks fail under Rosetta; F9 hangs | Native **arm64** launcher (`run_bepinex_arm64.sh`) |
| Game binary lacks the JIT entitlement CoreCLR needs on Apple Silicon (BepInEx never loads, or instant crash) | Launcher auto-builds a re-signed **shadow bundle** with JIT entitlements (`~/fm26_bep/fm.app`) |
| `ClassInjector` cannot add MonoBehaviours on arm64 | **`ExportDriver`** driven by BepInEx `MainThreadTick` |
| Virtualised list scroll overshoots | Scroll **one third of the viewport** per step |
| Crashes around 300–400 exported rows | Configurable **`ScrollStepDelayFrames`** (default **18**) + error **retry** |
| UITK “dirty repaint” mid-capture | Catch and retry instead of aborting |
| Empty cells in FM custom views | Read SI.Bindable text correctly |

Full BepInEx patch notes: [`docs/BEPINEX-PATCH.md`](docs/BEPINEX-PATCH.md).

---

## Requirements

- **macOS 13+** on **Apple Silicon** (M1/M2/M3/M4)
- **Football Manager 26** (Steam)
- **BepInEx 6 IL2CPP** already installed in your game folder ([BepInEx docs](https://docs.bepinex.dev/))
- **~5 GB free disk** on your boot volume (BepInEx interop cache + export temp files)
- Terminal access (one-time setup)

---

## Installation (recommended)

### Step 1 — Install stock BepInEx

Follow any standard FM26 BepInEx IL2CPP guide. After install you should have:

```
Football Manager 26/
  BepInEx/
  run_bepinex.sh
  doorstop_config.ini
  …
```

Launch the game **once** through BepInEx and reach the main menu so interop assemblies are generated (stored under `~/fm26_bep/interop` when using our launcher).

### Step 2 — Run the macOS compatibility installer

Clone or download this repo, then:

```bash
export FM26_GAME="$HOME/Library/Application Support/Steam/steamapps/common/Football Manager 26"
# ↑ adjust if your game lives elsewhere (external SSD, etc.)

bash install_macos.sh
```

This will:

1. Install an arm64 .NET runtime + native launcher script into your game folder
2. Patch BepInEx core with `MainThreadTick` (backs up stock DLLs first)
3. Install our `FM26PlayerExport.dll` into `BepInEx/plugins/`

### Step 3 — Views: install TFP presets or make your own

FM exports only the columns visible in your current table view. You need a view with enough attributes for meaningful analysis — either use ours or build your own.

**Option A — TFP presets (recommended for [TFP FM](https://github.com/DadMych/tfp_fm), currently in development)**

Copy the bundled `.fmf` files into your FM26 views folder (Finder: **Library → Application Support → Sports Interactive → Football Manager 26 → views**):

```bash
VIEWS="$HOME/Library/Application Support/Sports Interactive/Football Manager 26/views"
mkdir -p "$VIEWS"
cp views/tfp_basic_stats.fmf views/tfp_fm_squad_v1.fmf "$VIEWS/"
```

| View file | Screen in FM26 |
|-----------|----------------|
| `tfp_basic_stats.fmf` | **Player Search** / scouting shortlists |
| `tfp_fm_squad_v1.fmf` | **Squad** |

(`install_macos.sh` copies these automatically.)

**Option B — Your own view**

Create or download any FM26 view with the columns you care about (Name, Age, Position, attributes, value, etc.). Save the `.fmf` into the same `views/` folder above, or build columns in-game via **Add Column** on the table header.

Minimum for most tools: **Name + Position + ~20 known attributes**. More visible columns (fewer `-` masks) = better scores.

**Loading a view in game:** right-click the table header → **Import View** → pick the file → **Load**.

### Step 4 — Always launch through arm64

**Do not** use Steam’s default launch on Apple Silicon — it may run under Rosetta and break hooks.

```bash
cd "$FM26_GAME" && ./run_bepinex_arm64.sh
```

**Steam launch options** (Properties → Launch Options):

```
"/full/path/to/Football Manager 26/run_bepinex_arm64.sh" %command%
```

### Step 5 — Export in game

1. Open the right screen and load a view (TFP presets or your own):
   - **Squad** → `tfp_fm_squad_v1` (or any squad view with full attributes)
   - **Player Search** → `tfp_basic_stats` (or your scouting view)
2. Select players (Ctrl+A for all visible rows).
3. Press **F9** (or Ctrl+P) and **do not touch the mouse** until export finishes.
4. Find the CSV here (macOS default for **FM26 Player Export by vinteset**):

   ```
   ~/Sports Interactive/Football Manager 26/FM26PlayerExport by vinteset/Exports CSV/
   ```

   HTML copies land in `Exports HTML/` in the same folder.

   > Some guides mention `~/Documents/Sports Interactive/…` — on macOS the plugin usually writes to **`~/Sports Interactive/`** (no `Documents`). Check both if you cannot find the file.

Large lists take time: ~0.3 s per scroll step at default settings (657 rows ≈ several minutes).

---

## Configuration

After the first export, edit:

```
Football Manager 26/BepInEx/config/com.koda.fm26.playerexport.cfg
```

```ini
[Export]
# UI hard cap is ~10000 rows
MaxRowsToExport = 10000

# Frames to wait after each scroll step (~60 fps).
# Default 18 ≈ 0.3 s/step. Raise if you still crash on long lists.
ScrollStepDelayFrames = 18
```

Delete the `.cfg` file to regenerate defaults.

---

## Build from source (optional)

Only needed if you change the plugin or `dist/` is missing.

```bash
export FM26_GAME="/path/to/Football Manager 26"
export FM26_BEP="$HOME/fm26_bep"   # interop cache from a prior BepInEx boot

bash build_plugin.sh
cp plugin/bin/Release/net6.0/FM26PlayerExport.dll dist/FM26PlayerExport.dll
bash install_macos.sh
```

Requires [.NET SDK 6+](https://dotnet.microsoft.com/download).

| Variable | Purpose |
|----------|---------|
| `FM26_GAME` | Game install root |
| `FM26_BEP` | APFS cache root (default `~/fm26_bep`) |
| `FM26_INTEROP_DIR` | Il2Cpp interop assemblies (default `$FM26_BEP/interop`) |
| `FM26_CORE_DIR` | BepInEx core DLLs (default `$FM26_GAME/BepInEx/core`) |

---

## Publishing releases

Tag a version and attach `dist/` as release assets (or rely on the committed binaries in `dist/` — ~250 KB total). Point users to:

```bash
git clone https://github.com/DadMych/fm26-player-export-macos.git
cd fm26-player-export-macos
FM26_GAME="/path/to/Football Manager 26" bash install_macos.sh
```

---

## Troubleshooting

| Symptom | What to do |
|---------|------------|
| `dyld: … libdoorstop.dylib … (have 'x86_64,arm64', need 'arm64e')`, no logs at all | Update to the latest launcher (`git pull`, re-run `install_macos.sh`). Older versions exported `DYLD_INSERT_LIBRARIES` globally, and with SIP disabled dyld tried to inject doorstop into `/usr/bin/arch` (an arm64e system binary) instead of the game. The current launcher passes DYLD vars only to the game process via `arch -e`. |
| Game exits silently right after launch, log ends after `[UnityMemory] Configuration Parameters`, no crash report | You launched the script from another directory. Steam's DRM check silently exits the game when CWD isn't the game folder. Update to the latest launcher (it does `cd` itself), or run the script from inside the game folder. |
| Game boots normally but **no BepInEx at all** (no interop, no `LogOutput.log`), or crashes instantly during load | The stock FM binary is signed by SEGA **without the JIT entitlement** that .NET CoreCLR needs on Apple Silicon, so injection either gets stripped or dies in `pthread_jit_write_protect_np`. Update to the latest launcher (`git pull`, re-run `install_macos.sh`): it now builds a re-signed "shadow" copy of the game binary (in `~/fm26_bep/fm.app`) with the JIT entitlements added, automatically, on every launch. Also check the game's own output at `/tmp/fm26_arm64_launch.log` — the launcher prints this path. |
| F9 does nothing | Launch via `run_bepinex_arm64.sh`, not plain Steam |
| Log stops at “Runtime invoke patched” | Rosetta path — switch to arm64 launcher |
| Crash around row 300–400 | Free disk space; set `ScrollStepDelayFrames = 24` or `30` |
| Export finishes early | Scroll the list manually before F9; ensure rows are selected |
| Empty / `-` attributes | Use a view with full attribute columns |
| `No space left on device` | BepInEx cannot flush logs or write CSV — free several GB |

Logs: `Football Manager 26/BepInEx/LogOutput.log` — look for `[FM26Export]` lines.

---

## Support

If this saved you a few hours of crashing, a coffee helps keep [TFP FM / tfpdev](https://buymeacoffee.com/tfpdev) going:

**[buymeacoffee.com/tfpdev](https://buymeacoffee.com/tfpdev)**

No paywall on the plugin — MIT, free forever.

---

## License

MIT for our patches — see [`LICENSE`](LICENSE) and [`NOTICE.md`](NOTICE.md).

Original **FM26 Player Export** © vinteset (community plugin). **BepInEx** © BepInEx team (LGPL-2.1). **Football Manager** © Sports Interactive / SEGA. Not affiliated.
