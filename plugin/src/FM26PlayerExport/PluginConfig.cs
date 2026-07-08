using System;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;

namespace FM26PlayerExport;

public static class PluginConfig
{
	public const int DefaultMaxRowsToExport = 5000;

	public const int SafeHardMaxRowsToExport = 10000;

	private const int DefaultMaxScrollAttempts = 500;

	private const int SafeHardMaxScrollAttempts = 20000;

	public const int DefaultScrollStepDelayFrames = 18;

	public static ConfigEntry<int> MaxRowsToExport;

	public static ConfigEntry<int> ScrollStepDelayFrames;

	[field: CompilerGenerated]
	public static int EffectiveMaxRowsToExport
	{
		[CompilerGenerated]
		get;
		[CompilerGenerated]
		private set;
	} = 5000;

	public static int EffectiveScrollStepDelayFrames { get; private set; } = DefaultScrollStepDelayFrames;

	public static void Init(ConfigFile config, ManualLogSource log)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Expected O, but got Unknown
		MaxRowsToExport = config.Bind<int>("Export", "MaxRowsToExport", 5000, new ConfigDescription("Maximum number of selected rows exported by list handlers. FM26 player lists currently present only the first 10000 rows in the UI, so higher values are clamped for safety.", (AcceptableValueBase)(object)new AcceptableValueRange<int>(1, 10000), global::System.Array.Empty<object>()));
		ScrollStepDelayFrames = config.Bind<int>("Export", "ScrollStepDelayFrames", DefaultScrollStepDelayFrames, new ConfigDescription("Frames to wait after each scroll step before reading rows. Higher = slower but more stable export (fewer crashes on long lists); the game runs ~60 fps, so 18 frames is ~0.3 s per step.", (AcceptableValueBase)(object)new AcceptableValueRange<int>(1, 120), global::System.Array.Empty<object>()));
		EffectiveScrollStepDelayFrames = Math.Max(1, Math.Min(ScrollStepDelayFrames.Value, 120));
		log?.LogInfo((object)("[FM26Export.CONFIG] ScrollStepDelayFrames=" + EffectiveScrollStepDelayFrames + "."));
		Refresh(log);
	}

	public static int GetMaxScrollAttemptsForRows(int maxRows)
	{
		// Scroll now advances a third of a viewport per step (virtualised-list fix),
		// so allow ~3x the attempts the full-viewport stepping needed.
		int num = maxRows / 2 + 300;
		if (num < 500)
		{
			return 500;
		}
		if (num > 20000)
		{
			return 20000;
		}
		return num;
	}

	private static void Refresh(ManualLogSource log)
	{
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Expected O, but got Unknown
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Expected O, but got Unknown
		int num = ((MaxRowsToExport != null) ? MaxRowsToExport.Value : 5000);
		int num2 = Math.Max(1, Math.Min(num, 10000));
		bool flag = default(bool);
		ManualLogSource val;
		if (MaxRowsToExport != null && num != num2)
		{
			MaxRowsToExport.Value = num2;
			val = log;
			if (val != null)
			{
				BepInExWarningLogInterpolatedStringHandler val2 = new BepInExWarningLogInterpolatedStringHandler(77, 1, out flag);
				if (flag)
				{
					((BepInExLogInterpolatedStringHandler)val2).AppendLiteral("[FM26Export.CONFIG] MaxRowsToExport fora do intervalo seguro; ajustado para ");
					((BepInExLogInterpolatedStringHandler)val2).AppendFormatted<int>(num2);
					((BepInExLogInterpolatedStringHandler)val2).AppendLiteral(".");
				}
				val.LogWarning(val2);
			}
		}
		EffectiveMaxRowsToExport = num2;
		val = log;
		if (val != null)
		{
			BepInExInfoLogInterpolatedStringHandler val3 = new BepInExInfoLogInterpolatedStringHandler(37, 1, out flag);
			if (flag)
			{
				((BepInExLogInterpolatedStringHandler)val3).AppendLiteral("[FM26Export.CONFIG] MaxRowsToExport=");
				((BepInExLogInterpolatedStringHandler)val3).AppendFormatted<int>(EffectiveMaxRowsToExport);
				((BepInExLogInterpolatedStringHandler)val3).AppendLiteral(".");
			}
			val.LogInfo(val3);
		}
	}
}
