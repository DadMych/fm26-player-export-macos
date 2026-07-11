using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace FM26DisplayFix;

[BepInPlugin("com.tfpdev.fm26displayfix", "FM26 Display Fix", "1.0.1")]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log = null!;

    public static ConfigEntry<bool> Enabled = null!;
    public static ConfigEntry<bool> ForceNativeAspect = null!;
    public static ConfigEntry<int> OverrideWidth = null!;
    public static ConfigEntry<int> OverrideHeight = null!;
    public static ConfigEntry<bool> PatchMatchCamera = null!;
    public static ConfigEntry<string> SkipExpansionElements = null!;

    public override void Load()
    {
        Log = base.Log;

        Enabled = Config.Bind("General", "Enabled", true,
            "Enable display scaling for non-16:9 monitors (16:10 MacBooks, ultrawide, etc.)");
        ForceNativeAspect = Config.Bind("General", "ForceNativeAspect", true,
            "Force the game's render resolution to match the display aspect ratio. " +
            "FM26 only offers 16:9 resolutions, which macOS letterboxes with black bars on 16:10/ultrawide displays.");
        OverrideWidth = Config.Bind("Resolution", "Width", 0,
            "Override render width in pixels (0 = auto-detect display safe area)");
        OverrideHeight = Config.Bind("Resolution", "Height", 0,
            "Override render height in pixels (0 = auto-detect display safe area)");
        PatchMatchCamera = Config.Bind("Patches", "PatchMatchCamera", true,
            "Correct match-engine camera aspect ratio");
        SkipExpansionElements = Config.Bind("Patches", "SkipExpansionElements",
            "ModalDialog,GenericModalDialog,ExternalNewsDynamicCard",
            "Comma-separated UI element names (or Prefix*) excluded from layout expansion. " +
            "Do not add 'Card' — Portal/dashboard widgets use that name.");

        if (!Enabled.Value)
        {
            Log.LogInfo("FM26 Display Fix disabled via config.");
            return;
        }

        try
        {
            Harmony.CreateAndPatchAll(typeof(DisplayPatches), "com.tfpdev.fm26displayfix");
            Log.LogInfo("Harmony patches applied.");
        }
        catch (Exception ex)
        {
            Log.LogError($"Harmony patching failed: {ex}");
        }

        ClassInjector.RegisterTypeInIl2Cpp<PanelScaler>();
        var go = new GameObject("FM26DisplayFix_PanelScaler");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<PanelScaler>();

        Log.LogInfo("FM26 Display Fix loaded (16:10 + ultrawide).");
    }
}
