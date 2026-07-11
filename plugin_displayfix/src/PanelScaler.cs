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
    internal static float LastRefResHeight { get; private set; }
    internal static bool LastIsUltrawide { get; private set; }
    internal static bool LastIsTall { get; private set; }

    void LateUpdate()
    {
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

        float scale = LastIsTall ? screenW / refRes.x : screenH / refRes.y;
        LastHeightRatio = scale;
        LastRefResWidth = refRes.x;
        LastRefResHeight = refRes.y;

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
    private static readonly Dictionary<long, float> s_tileOriginalHeights = new Dictionary<long, float>();
    private static readonly Dictionary<long, float> s_tileOriginalTops = new Dictionary<long, float>();

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
                ScanForGridLayouts(root, 0, scaleWidth: true, scaleHeight: false);
            }

            if (LastIsTall)
            {
                ExpandTallScreenChrome(root, 0);
                ExpandElementVertical(root, 0, heightThreshold, false);
                ScanForGridLayouts(root, 0, scaleWidth: false, scaleHeight: true);
            }
        }
    }

    /// <summary>
    /// Pin in-game screen bodies below the nav bar and stretch dashboard content vertically.
    /// </summary>
    private static void ExpandTallScreenChrome(VisualElement ve, int depth)
    {
        if (ve == null || depth > 40) return;

        try
        {
            if (ve.name == "Body")
            {
                float offsetY = 0f;
                try { offsetY = ve.layout.y; } catch { }
                if (offsetY > 2f)
                {
                    ve.style.height = StyleKeyword.Null;
                    ve.style.maxHeight = StyleKeyword.None;
                    ve.style.bottom = new StyleLength(new Length(0f, LengthUnit.Pixel));
                    ve.style.overflow = Overflow.Hidden;
                }
            }

            if (ve.name == "PortalScreen" || ve.name == "Overview" || ve.name == "Content")
            {
                ve.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));
                ve.style.maxHeight = StyleKeyword.None;
                ve.style.flexGrow = 1f;
                ve.style.overflow = Overflow.Hidden;
            }

            // Kill nested scroll when the parent already fits the viewport.
            if (ve is ScrollView sv)
            {
                sv.verticalScrollerVisibility = ScrollerVisibility.Hidden;
                sv.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                sv.mode = ScrollViewMode.Vertical;
                var content = sv.contentContainer;
                if (content != null)
                {
                    content.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));
                    content.style.minHeight = StyleKeyword.None;
                }
            }
        }
        catch { }

        for (int i = 0; i < ve.childCount; i++)
            ExpandTallScreenChrome(ve[i], depth + 1);
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
                float offsetY = 0f;
                try { offsetY = ve.layout.y; } catch { }
                if (offsetY <= 2f)
                {
                    ve.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));
                    ve.style.maxHeight = StyleKeyword.None;
                    ve.style.minHeight = StyleKeyword.None;
                }
                else
                {
                    ve.style.height = StyleKeyword.Null;
                    ve.style.maxHeight = StyleKeyword.None;
                    ve.style.bottom = new StyleLength(new Length(0f, LengthUnit.Pixel));
                }
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
                if (h >= threshold && !HasVisibleVerticalSibling(ve))
                {
                    ve.style.maxHeight = StyleKeyword.None;
                    ve.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));
                    ve.style.flexGrow = 1f;
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

    private static void ScanForGridLayouts(VisualElement ve, int depth, bool scaleWidth, bool scaleHeight)
    {
        if (ve == null || depth > 35) return;
        if (ve.name == "GridLayoutElementContent")
            ScaleGridTiles(ve, scaleWidth, scaleHeight);
        for (int i = 0; i < ve.childCount; i++)
            ScanForGridLayouts(ve[i], depth + 1, scaleWidth, scaleHeight);
    }

    private static void ScaleGridTiles(VisualElement container, bool scaleWidth, bool scaleHeight)
    {
        if (LastRefResWidth <= 0f || LastRefResHeight <= 0f || LastHeightRatio <= 0f) return;

        float logicalCanvasW = Screen.width / LastHeightRatio;
        float logicalCanvasH = Screen.height / LastHeightRatio;
        float ratioW = logicalCanvasW / LastRefResWidth;
        float ratioH = logicalCanvasH / LastRefResHeight;

        if (scaleWidth && ratioW < 1.02f) scaleWidth = false;

        // On tall screens the logical canvas height often matches the 16:9 reference, but
        // dashboard tiles are absolutely positioned for a shorter content band — stretch
        // them to fill the grid container instead of leaving a scrollable gap.
        if (scaleHeight)
            ratioH = ComputeGridHeightRatio(container, ratioH);

        if (scaleHeight && ratioH < 1.02f) scaleHeight = false;
        if (!scaleWidth && !scaleHeight) return;

        for (int i = 0; i < container.childCount; i++)
        {
            var child = container[i];
            if (child == null) continue;
            try
            {
                long key = child.Pointer.ToInt64();
                var ws = child.style.width;
                if (ws.keyword == StyleKeyword.Undefined
                    && ws.value.unit == LengthUnit.Percent)
                    continue;

                if (ws.keyword == StyleKeyword.Undefined
                    && ws.value.unit == LengthUnit.Pixel
                    && ws.value.value > 0f)
                {
                    float baseW = s_tileOriginalWidths.TryGetValue(key, out float sw) ? sw : ws.value.value;
                    if (!s_tileOriginalWidths.ContainsKey(key)) s_tileOriginalWidths[key] = ws.value.value;
                    if (scaleWidth)
                        child.style.width = new StyleLength(new Length(baseW * ratioW, LengthUnit.Pixel));
                }

                var ls = child.style.left;
                if (scaleWidth
                    && ls.keyword == StyleKeyword.Undefined
                    && ls.value.unit == LengthUnit.Pixel
                    && ls.value.value > 0f)
                {
                    float baseL = s_tileOriginalLefts.TryGetValue(key, out float sl) ? sl : ls.value.value;
                    if (!s_tileOriginalLefts.ContainsKey(key)) s_tileOriginalLefts[key] = ls.value.value;
                    child.style.left = new StyleLength(new Length(baseL * ratioW, LengthUnit.Pixel));
                }

                var hs = child.style.height;
                if (scaleHeight
                    && hs.keyword == StyleKeyword.Undefined
                    && hs.value.unit == LengthUnit.Pixel
                    && hs.value.value > 0f)
                {
                    float baseH = s_tileOriginalHeights.TryGetValue(key, out float sh) ? sh : hs.value.value;
                    if (!s_tileOriginalHeights.ContainsKey(key)) s_tileOriginalHeights[key] = hs.value.value;
                    child.style.height = new StyleLength(new Length(baseH * ratioH, LengthUnit.Pixel));
                }

                var ts = child.style.top;
                if (scaleHeight
                    && ts.keyword == StyleKeyword.Undefined
                    && ts.value.unit == LengthUnit.Pixel
                    && ts.value.value > 0f)
                {
                    float baseT = s_tileOriginalTops.TryGetValue(key, out float st) ? st : ts.value.value;
                    if (!s_tileOriginalTops.ContainsKey(key)) s_tileOriginalTops[key] = ts.value.value;
                    child.style.top = new StyleLength(new Length(baseT * ratioH, LengthUnit.Pixel));
                }
            }
            catch { }
        }
    }

    private static float ComputeGridHeightRatio(VisualElement container, float aspectRatio)
    {
        try
        {
            float containerH = container.layout.height;
            if (containerH <= 1f)
            {
                var p = container.parent;
                if (p != null) containerH = p.layout.height;
            }
            if (containerH <= 1f) return aspectRatio;

            float designBottom = 0f;
            for (int i = 0; i < container.childCount; i++)
            {
                var child = container[i];
                if (child == null) continue;

                float top = 0f, height = 0f;
                try
                {
                    var ts = child.style.top;
                    if (ts.keyword == StyleKeyword.Undefined && ts.value.unit == LengthUnit.Pixel)
                        top = ts.value.value;
                    else
                        top = child.layout.y;
                }
                catch { }

                try
                {
                    var hs = child.style.height;
                    if (hs.keyword == StyleKeyword.Undefined && hs.value.unit == LengthUnit.Pixel)
                        height = hs.value.value;
                    else
                        height = child.layout.height;
                }
                catch { }

                if (height > 1f)
                    designBottom = Math.Max(designBottom, top + height);
            }

            if (designBottom > 1f && containerH > designBottom + 4f)
                return Math.Max(aspectRatio, containerH / designBottom);
        }
        catch { }

        return aspectRatio;
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

        try
        {
            var p = ve.parent;
            if (p == null) return false;
            float myY = ve.layout.y;
            long myPtr = ve.Pointer.ToInt64();
            for (int i = 0; i < p.childCount; i++)
            {
                var sib = p[i];
                if (sib == null) continue;
                if (sib.Pointer.ToInt64() == myPtr) continue;
                if (sib.layout.width <= 1f) continue;
                if (Math.Abs(sib.layout.y - myY) < 2f) return true;
            }
        }
        catch { }

        return false;
    }

    private static bool HasVisibleVerticalSibling(VisualElement ve)
    {
        try
        {
            var p = ve.parent;
            if (p == null) return false;
            float myY = ve.layout.y;
            long myPtr = ve.Pointer.ToInt64();
            for (int i = 0; i < p.childCount; i++)
            {
                var sib = p[i];
                if (sib == null) continue;
                if (sib.Pointer.ToInt64() == myPtr) continue;
                if (sib.layout.width <= 1f || sib.layout.height <= 1f) continue;
                if (Math.Abs(sib.layout.y - myY) >= 2f) return true;
            }
        }
        catch { }
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
