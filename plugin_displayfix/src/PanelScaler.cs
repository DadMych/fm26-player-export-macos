using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace FM26DisplayFix;

/// <summary>
/// Scales FM26's UI Toolkit panels for non-16:9 displays.
/// Ultrawide (21:9+): horizontal expansion (from LionelFW/fm26ultrawidefix).
/// Tall (16:10 MacBooks, 3:2): panel scale + vertical expansion for bottom gap.
/// </summary>
public class PanelScaler : MonoBehaviour
{
    public PanelScaler(nint ptr) : base(ptr) { }

    private const float RefAspect = 16f / 9f;
    private const float AspectTolerance = 0.02f;

    private int _cameraPollFrame;
    private int _uiExpandFrame;
    private int _resolutionPollFrame = 110;

    internal static float LastHeightRatio { get; private set; } = 1f;
    internal static float LastRefResWidth { get; private set; }
    internal static bool LastIsUltrawide { get; private set; }
    internal static bool LastIsTall { get; private set; }

    void LateUpdate()
    {
        // FM26 only offers 16:9 resolutions; on 16:10/ultrawide displays the OS
        // letterboxes the render. Re-check periodically because the game can reset
        // the resolution (preferences screen, alt-tab, display change).
        if (Plugin.ForceNativeAspect.Value && ++_resolutionPollFrame >= 120)
        {
            _resolutionPollFrame = 0;
            TryForceNativeAspect();
        }

        if (Plugin.PatchMatchCamera.Value)
            FixCameraAspects();

        if (++_uiExpandFrame >= 30)
        {
            _uiExpandFrame = 0;
            ExpandUIDocuments();
        }
    }

    internal static bool TryGetNativeResolution(out int w, out int h)
    {
        w = Plugin.OverrideWidth.Value;
        h = Plugin.OverrideHeight.Value;
        if (w > 0 && h > 0) return true;

        try
        {
            var disp = Display.main;
            if (disp == null) return false;
            w = disp.systemWidth;
            h = disp.systemHeight;
            if (w <= 0 || h <= 0) return false;

            // Notched MacBook panels report ~1.54 aspect, but macOS only gives
            // fullscreen apps the area below the notch/menu bar, which Apple made
            // exactly 16:10. Rendering taller than that gets letterboxed on all sides.
            float aspect = (float)w / h;
            if (aspect > 1.5f && aspect < 1.58f)
                h = Mathf.RoundToInt(w / 1.6f);

            return true;
        }
        catch { return false; }
    }

    internal static void RewriteResolution(ref int width, ref int height, ref FullScreenMode mode)
    {
        if (!Plugin.ForceNativeAspect.Value) return;
        if (mode == FullScreenMode.Windowed) return;
        if (!TryGetNativeResolution(out int nw, out int nh)) return;
        if (width == nw && height == nh && mode == FullScreenMode.FullScreenWindow) return;

        Plugin.Log.LogInfo($"SetResolution intercepted: {width}x{height} ({mode}) -> {nw}x{nh} (FullScreenWindow)");
        width = nw;
        height = nh;
        mode = FullScreenMode.FullScreenWindow;
    }

    private static void TryForceNativeAspect()
    {
        try
        {
            if (!Screen.fullScreen) return;
            if (!TryGetNativeResolution(out int nw, out int nh)) return;
            if (Screen.width == nw && Screen.height == nh
                && Screen.fullScreenMode == FullScreenMode.FullScreenWindow) return;

            Rect safe = default;
            try { safe = Screen.safeArea; } catch { }
            Plugin.Log.LogInfo(
                $"ForceNativeAspect: game {Screen.width}x{Screen.height} ({Screen.fullScreenMode}) -> {nw}x{nh} (FullScreenWindow); " +
                $"system {Display.main.systemWidth}x{Display.main.systemHeight}, safeArea {safe.width:F0}x{safe.height:F0}@{safe.x:F0},{safe.y:F0}");
            Screen.SetResolution(nw, nh, FullScreenMode.FullScreenWindow);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"ForceNativeAspect failed: {ex.Message}");
        }
    }

    internal static void ApplyScaling(PanelSettings settings)
    {
        if (settings == null) return;

        float screenW = Screen.width;
        float screenH = Screen.height;
        var refRes = settings.referenceResolution;
        if (refRes.x <= 0f || refRes.y <= 0f) return;

        float screenAspect = screenW / screenH;
        float refAspect = refRes.x / refRes.y;

        if (Math.Abs(screenAspect - refAspect) <= AspectTolerance
            && Math.Abs(screenAspect - RefAspect) <= AspectTolerance)
            return;

        LastIsUltrawide = screenAspect > RefAspect + AspectTolerance;
        LastIsTall = screenAspect < RefAspect - AspectTolerance;

        // Scale by the constrained dimension so the reference layout fits exactly:
        // ultrawide -> height (then expand sideways), tall -> width (then expand down).
        float scale = LastIsTall ? screenW / refRes.x : screenH / refRes.y;
        LastHeightRatio = scale;
        LastRefResWidth = refRes.x;

        settings.scaleMode = PanelScaleMode.ConstantPixelSize;
        settings.scale = scale;

        Plugin.Log.LogDebug(
            $"Panel scale {scale:F3} ({screenW:F0}x{screenH:F0}, aspect {screenAspect:F3}, ultrawide={LastIsUltrawide}, tall={LastIsTall})");
    }

    private void FixCameraAspects()
    {
        if (++_cameraPollFrame < 30) return;
        _cameraPollFrame = 0;

        float targetAspect = (float)Screen.width / Screen.height;
        foreach (var cam in Camera.allCameras)
        {
            if (cam == null) continue;
            if (Math.Abs(cam.aspect - targetAspect) < 0.005f) continue;

            if (!cam.orthographic && cam.aspect > 0f)
            {
                float origFovRad = cam.fieldOfView * Mathf.Deg2Rad;
                float newFovRad = 2f * (float)Math.Atan(Math.Tan(origFovRad / 2f) * (cam.aspect / targetAspect));
                cam.fieldOfView = newFovRad * Mathf.Rad2Deg;
            }

            cam.aspect = targetAspect;
        }
    }

    private static readonly HashSet<string> s_skipExact = new HashSet<string>();
    private static readonly List<string> s_skipPrefixes = new List<string>();
    private static readonly Dictionary<long, float> s_tileOriginalWidths = new Dictionary<long, float>();
    private static readonly Dictionary<long, float> s_tileOriginalLefts = new Dictionary<long, float>();

    private static void RefreshSkipNames()
    {
        s_skipExact.Clear();
        s_skipPrefixes.Clear();
        var raw = Plugin.SkipExpansionElements?.Value ?? "";
        foreach (var part in raw.Split(','))
        {
            var n = part.Trim();
            if (n.Length == 0) continue;
            if (n.EndsWith("*"))
                s_skipPrefixes.Add(n.Substring(0, n.Length - 1));
            else
                s_skipExact.Add(n);
        }
    }

    private static bool IsSkipped(string name)
    {
        if (name == null) return false;
        if (s_skipExact.Contains(name)) return true;
        foreach (var prefix in s_skipPrefixes)
            if (name.StartsWith(prefix, StringComparison.Ordinal)) return true;
        return false;
    }

    private static void ExpandUIDocuments()
    {
        float aspect = (float)Screen.width / Screen.height;
        if (Math.Abs(aspect - RefAspect) < AspectTolerance) return;

        RefreshSkipNames();

        float logicalCanvasW = LastHeightRatio > 0f ? Screen.width / LastHeightRatio : Screen.width;
        float logicalCanvasH = LastHeightRatio > 0f ? Screen.height / LastHeightRatio : Screen.height;
        float widthThreshold = logicalCanvasW * 0.4f;
        float heightThreshold = logicalCanvasH * 0.4f;

        UIDocument[] docs;
        try
        {
            docs = GameObject.FindObjectsOfType<UIDocument>();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogDebug($"ExpandUIDocuments: {ex.Message}");
            return;
        }

        foreach (var doc in docs)
        {
            if (doc == null) continue;
            var root = doc.rootVisualElement;
            if (root == null) continue;

            if (LastIsUltrawide)
            {
                ExpandElementHorizontal(root, 0, widthThreshold, false);
                ExpandCardTemplates(root, 0);
                ScanForGridLayouts(root, 0);
            }

            if (LastIsTall)
                ExpandElementVertical(root, 0, heightThreshold, false);
        }
    }

    private static void ExpandElementHorizontal(VisualElement ve, int depth, float threshold, bool skipExpansion)
    {
        if (ve == null || depth > 100) return;

        bool childrenSkip = skipExpansion;

        if (!skipExpansion)
        {
            if (depth <= 1)
            {
                ForceFullWidth(ve);
                if (depth == 0)
                    ve.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));

                if (ve.name != null && IsSkipped(ve.name))
                    childrenSkip = true;
            }
            else if (ve.name != null && IsSkipped(ve.name))
            {
                childrenSkip = true;
            }
            else
            {
                float w = TryGetLayoutWidth(ve);
                if (w >= threshold && !ParentIsRowFlex(ve))
                {
                    ve.style.maxWidth = StyleKeyword.None;
                    ve.style.width = new StyleLength(new Length(100f, LengthUnit.Percent));
                    ve.style.marginLeft = new StyleLength(new Length(0f, LengthUnit.Pixel));
                    ve.style.marginRight = new StyleLength(new Length(0f, LengthUnit.Pixel));
                }
            }
        }

        for (int i = 0; i < ve.childCount; i++)
            ExpandElementHorizontal(ve[i], depth + 1, threshold, childrenSkip);
    }

    private static void ExpandElementVertical(VisualElement ve, int depth, float threshold, bool skipExpansion)
    {
        if (ve == null || depth > 100) return;

        bool childrenSkip = skipExpansion;

        if (!skipExpansion)
        {
            if (depth <= 2)
            {
                ve.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));
                ve.style.maxHeight = StyleKeyword.None;
                ve.style.minHeight = StyleKeyword.None;
                if (depth <= 1)
                    ForceFullWidth(ve);

                if (ve.name != null && IsSkipped(ve.name))
                    childrenSkip = true;
            }
            else if (ve.name != null && IsSkipped(ve.name))
            {
                childrenSkip = true;
            }
            else
            {
                float h = TryGetLayoutHeight(ve);
                if (h >= threshold && !ParentIsColumnFlex(ve))
                {
                    ve.style.maxHeight = StyleKeyword.None;
                    ve.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));
                    ve.style.marginTop = new StyleLength(new Length(0f, LengthUnit.Pixel));
                    ve.style.marginBottom = new StyleLength(new Length(0f, LengthUnit.Pixel));
                }
            }
        }

        for (int i = 0; i < ve.childCount; i++)
            ExpandElementVertical(ve[i], depth + 1, threshold, childrenSkip);
    }

    private static void ExpandCardTemplates(VisualElement ve, int depth)
    {
        if (ve == null || depth > 50) return;

        if (LooksLikeCardTemplate(ve))
        {
            float canvasW = LastHeightRatio > 0f ? Screen.width / LastHeightRatio : Screen.width;
            ApplyExplicitWidth(ve, canvasW);
            for (int i = 0; i < ve.childCount; i++)
            {
                var child = ve[i];
                if (child != null) ApplyExplicitWidth(child, canvasW);
            }
            return;
        }

        for (int i = 0; i < ve.childCount; i++)
            ExpandCardTemplates(ve[i], depth + 1);
    }

    private static bool LooksLikeCardTemplate(VisualElement ve)
    {
        if (ve.childCount < 2) return false;
        for (int i = 0; i < ve.childCount; i++)
        {
            var child = ve[i];
            if (child != null && child.name == "Border") return true;
        }
        return false;
    }

    private static void ApplyExplicitWidth(VisualElement ve, float pixelWidth)
    {
        try
        {
            ve.style.paddingLeft = new StyleLength(new Length(0f, LengthUnit.Pixel));
            ve.style.paddingRight = new StyleLength(new Length(0f, LengthUnit.Pixel));
            ve.style.marginLeft = new StyleLength(new Length(0f, LengthUnit.Pixel));
            ve.style.marginRight = new StyleLength(new Length(0f, LengthUnit.Pixel));
            ve.style.width = new StyleLength(new Length(pixelWidth, LengthUnit.Pixel));
            ve.style.maxWidth = StyleKeyword.None;
            ve.style.overflow = Overflow.Visible;
        }
        catch { }
    }

    private static void ScanForGridLayouts(VisualElement ve, int depth)
    {
        if (ve == null || depth > 35) return;
        if (ve.name == "GridLayoutElementContent")
            ScaleGridTiles(ve);
        for (int i = 0; i < ve.childCount; i++)
            ScanForGridLayouts(ve[i], depth + 1);
    }

    private static void ScaleGridTiles(VisualElement container)
    {
        if (LastRefResWidth <= 0f || LastHeightRatio <= 0f) return;

        float logicalCanvasW = Screen.width / LastHeightRatio;
        float ratio = logicalCanvasW / LastRefResWidth;
        if (ratio < 1.05f) return;

        VisualElement percentChild = null;
        for (int i = 0; i < container.childCount; i++)
        {
            var c = container[i];
            if (c == null) continue;
            try
            {
                var ws = c.style.width;
                if (ws.keyword == StyleKeyword.Undefined
                    && ws.value.unit == LengthUnit.Percent
                    && percentChild == null)
                    percentChild = c;
            }
            catch { }
        }
        if (percentChild != null) return;

        for (int i = 0; i < container.childCount; i++)
        {
            var child = container[i];
            if (child == null) continue;
            try
            {
                long key = child.Pointer.ToInt64();
                var ws = child.style.width;
                if (ws.keyword == StyleKeyword.Undefined
                    && ws.value.unit == LengthUnit.Pixel
                    && ws.value.value > 0f)
                {
                    float baseW = s_tileOriginalWidths.TryGetValue(key, out float sw) ? sw : ws.value.value;
                    if (!s_tileOriginalWidths.ContainsKey(key)) s_tileOriginalWidths[key] = ws.value.value;
                    child.style.width = new StyleLength(new Length(baseW * ratio, LengthUnit.Pixel));
                }

                var ls = child.style.left;
                if (ls.keyword == StyleKeyword.Undefined
                    && ls.value.unit == LengthUnit.Pixel
                    && ls.value.value > 0f)
                {
                    float baseL = s_tileOriginalLefts.TryGetValue(key, out float sl) ? sl : ls.value.value;
                    if (!s_tileOriginalLefts.ContainsKey(key)) s_tileOriginalLefts[key] = ls.value.value;
                    child.style.left = new StyleLength(new Length(baseL * ratio, LengthUnit.Pixel));
                }
            }
            catch { }
        }
    }

    private static void ForceFullWidth(VisualElement ve)
    {
        ve.style.width = new StyleLength(new Length(100f, LengthUnit.Percent));
        ve.style.maxWidth = StyleKeyword.None;
        ve.style.marginLeft = new StyleLength(new Length(0f, LengthUnit.Pixel));
        ve.style.marginRight = new StyleLength(new Length(0f, LengthUnit.Pixel));
    }

    private static bool ParentIsRowFlex(VisualElement ve)
    {
        try
        {
            var p = ve.parent;
            if (p == null) return false;
            return p.resolvedStyle.flexDirection == FlexDirection.Row;
        }
        catch
        {
            try
            {
                var p = ve.parent;
                if (p == null) return false;
                var fd = p.style.flexDirection;
                if (fd.keyword == StyleKeyword.Undefined)
                    return fd.value == FlexDirection.Row;
            }
            catch { }
        }
        return false;
    }

    private static bool ParentIsColumnFlex(VisualElement ve)
    {
        try
        {
            var p = ve.parent;
            if (p == null) return false;
            return p.resolvedStyle.flexDirection == FlexDirection.Column;
        }
        catch
        {
            try
            {
                var p = ve.parent;
                if (p == null) return false;
                var fd = p.style.flexDirection;
                if (fd.keyword == StyleKeyword.Undefined)
                    return fd.value == FlexDirection.Column;
            }
            catch { }
        }
        return false;
    }

    private static float TryGetLayoutWidth(VisualElement ve)
    {
        try
        {
            float lw = ve.layout.width;
            if (!float.IsNaN(lw) && lw > 0f) return lw;
        }
        catch { }

        try
        {
            float rw = ve.resolvedStyle.width;
            if (!float.IsNaN(rw) && rw > 0f) return rw;
        }
        catch { }

        return -1f;
    }

    private static float TryGetLayoutHeight(VisualElement ve)
    {
        try
        {
            float lh = ve.layout.height;
            if (!float.IsNaN(lh) && lh > 0f) return lh;
        }
        catch { }

        try
        {
            float rh = ve.resolvedStyle.height;
            if (!float.IsNaN(rh) && rh > 0f) return rh;
        }
        catch { }

        return -1f;
    }
}
