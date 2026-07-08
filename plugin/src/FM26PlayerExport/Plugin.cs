using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace FM26PlayerExport;

[BepInPlugin("com.koda.fm26.playerexport", "FM26 Player Export (macOS)", "5.1.0-macos")]
public class Plugin : BasePlugin
{
	internal static ManualLogSource Log;

	private static ExportDriver _driver;

	public override void Load()
	{
		Log = base.Log;
		Log.LogInfo("[FM26Export v5.1-macos] Loaded. Hotkeys: [F9 or Ctrl+P] = export.");
		PluginConfig.Init(base.Config, Log);

		// arm64 path: no MonoBehaviour injection. Drive the export from BepInEx's main-thread pump.
		_driver = new ExportDriver();
		IL2CPPChainloader.MainThreadTick += _driver.Tick;
	}

	public override bool Unload()
	{
		if (_driver != null)
			IL2CPPChainloader.MainThreadTick -= _driver.Tick;
		_driver = null;
		return base.Unload();
	}
}
