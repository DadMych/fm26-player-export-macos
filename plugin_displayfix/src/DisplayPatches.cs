using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace FM26DisplayFix;

[HarmonyPatch]
internal static class DisplayPatches
{
    [HarmonyPatch(typeof(PanelSettings), nameof(PanelSettings.ApplyPanelSettings))]
    [HarmonyPostfix]
    static void PanelSettings_ApplyPanelSettings_Postfix(PanelSettings __instance)
    {
        PanelScaler.ApplyScaling(__instance);
    }

    // Intercept every resolution change so the game can't letterbox back to 16:9
    // (same approach as LionelFW/fm26ultrawidefix).
    [HarmonyPatch(typeof(Screen), nameof(Screen.SetResolution), typeof(int), typeof(int), typeof(FullScreenMode))]
    [HarmonyPrefix]
    static void Screen_SetResolution_Prefix(ref int width, ref int height, ref FullScreenMode fullscreenMode)
    {
        PanelScaler.RewriteResolution(ref width, ref height, ref fullscreenMode);
    }
}
