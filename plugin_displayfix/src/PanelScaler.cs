using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace FM26DisplayFix;

/// <summary>
/// Scales FM26's UI Toolkit panels for non-16:9 displays.
/// Ultrawide (21:9+): horizontal expansion (from LionelFW/fm26ultrawidefix).
/// Tall (16:10 MacBooks, 3:2): panel scale + dashboard vertical fill.
/// </summary>
public class PanelScaler : MonoBehaviour
{
    public PanelScaler(nint ptr) : base(ptr) { }

    private const float RefAspect = 16f / 9f;
    private const float AspectTolerance = 0.02f;
    /// <summary>Leave headroom at the bottom (shortcut bar / safe area). 0.97 = fill 97% of available height.</summary>
    private const float VerticalFillRatio = 0.97f;
    private const int ScreenApplyDelayFrames = 5;

    private int _cameraPollFrame;
    private int _uiExpandFrame;
    private int _overlayFrame;
    private int _resolutionPollFrame = 110;
    private bool _bodyPinned;
    private readonly Dictionary<long, int> _pendingScreens = new Dictionary<long, int>();
    private readonly HashSet<long> _chromeCompleted = new HashSet<long>();
    private readonly Dictionary<long, int> _pendingModals = new Dictionary<long, int>();
    private readonly HashSet<long> _modalChromeCompleted = new HashSet<long>();
    private readonly Dictionary<long, int> _pendingOverlays = new Dictionary<long, int>();
    private readonly HashSet<long> _overlayCompleted = new HashSet<long>();
    private const int ModalApplyDelayFrames = 3;
    private const int OverlayApplyDelayFrames = 3;

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
            ExpandUltrawideDocuments();
        }

        if (IsTallDisplay())
        {
            TickTallScreens();
            TickModals();
            TickOverlays();
        }

        if (++_overlayFrame >= 30)
        {
            _overlayFrame = 0;
            if (IsTallDisplay())
                ExpandOverlayDocuments();
        }
    }

    private static bool IsTallDisplay()
    {
        if (LastIsTall) return true;
        float aspect = (float)Screen.width / Screen.height;
        return aspect < RefAspect - AspectTolerance;
    }

    private void TickTallScreens()
    {
        RefreshSkipNames();

        UIDocument[] docs;
        try { docs = GameObject.FindObjectsOfType<UIDocument>(); }
        catch { return; }

        var visibleScreens = new List<VisualElement>();
        foreach (var doc in docs)
        {
            if (doc == null) continue;
            var root = doc.rootVisualElement;
            if (root == null) continue;

            if (!_bodyPinned)
            {
                var menu = FindDescendantNamed(root, "Menu");
                var body = menu != null ? FindChildNamed(menu, "Body") : null;
                if (body != null)
                {
                    PinScreenBody(body);
                    _bodyPinned = true;
                }
            }

            CollectVisibleDashboardScreens(root, visibleScreens);
        }

        var visibleKeys = new HashSet<long>();
        foreach (var screen in visibleScreens)
        {
            long key;
            try { key = screen.Pointer.ToInt64(); }
            catch { continue; }

            visibleKeys.Add(key);

            if (!_chromeCompleted.Contains(key))
            {
                if (!_pendingScreens.TryGetValue(key, out int remaining))
                {
                    _pendingScreens[key] = ScreenApplyDelayFrames;
                    continue;
                }

                if (remaining > 1)
                {
                    _pendingScreens[key] = remaining - 1;
                    continue;
                }

                _pendingScreens.Remove(key);
                ApplyDashboardChromeOnce(screen);
                _chromeCompleted.Add(key);
            }

            // Grids mount after the screen shell — retry until each grid is scaled once.
            ApplyScreenGridsOnly(screen, 0);
        }

        PrunePendingScreens(visibleKeys);
    }

    private void TickModals()
    {
        UIDocument[] docs;
        try { docs = GameObject.FindObjectsOfType<UIDocument>(); }
        catch { return; }

        var visibleModals = new List<VisualElement>();
        foreach (var doc in docs)
        {
            if (doc == null) continue;
            var root = doc.rootVisualElement;
            if (root == null) continue;
            CollectVisibleModals(root, visibleModals);
        }

        var visibleKeys = new HashSet<long>();
        foreach (var modal in visibleModals)
        {
            long key;
            try { key = modal.Pointer.ToInt64(); }
            catch { continue; }

            visibleKeys.Add(key);

            if (!_modalChromeCompleted.Contains(key))
            {
                if (!_pendingModals.TryGetValue(key, out int remaining))
                {
                    _pendingModals[key] = ModalApplyDelayFrames;
                    continue;
                }

                if (remaining > 1)
                {
                    _pendingModals[key] = remaining - 1;
                    continue;
                }

                _pendingModals.Remove(key);
                ApplyModalOnce(modal);
                _modalChromeCompleted.Add(key);
            }

            ApplyScreenGridsOnly(modal, 0);
        }

        if (_pendingModals.Count == 0) return;
        var stale = new List<long>();
        foreach (var key in _pendingModals.Keys)
        {
            if (!visibleKeys.Contains(key))
                stale.Add(key);
        }
        foreach (var key in stale)
            _pendingModals.Remove(key);
    }

    private void TickOverlays()
    {
        UIDocument[] docs;
        try { docs = GameObject.FindObjectsOfType<UIDocument>(); }
        catch { return; }

        var visibleCards = new List<VisualElement>();
        foreach (var doc in docs)
        {
            if (doc == null) continue;
            var root = doc.rootVisualElement;
            if (root == null) continue;
            CollectVisibleOverlayCards(root, visibleCards);
        }

        var visibleKeys = new HashSet<long>();
        foreach (var card in visibleCards)
        {
            long key;
            try { key = card.Pointer.ToInt64(); }
            catch { continue; }

            visibleKeys.Add(key);

            if (!_overlayCompleted.Contains(key))
            {
                if (!_pendingOverlays.TryGetValue(key, out int remaining))
                {
                    _pendingOverlays[key] = OverlayApplyDelayFrames;
                    continue;
                }

                if (remaining > 1)
                {
                    _pendingOverlays[key] = remaining - 1;
                    continue;
                }

                _pendingOverlays.Remove(key);
                ApplyOverlayCardOnce(card);
                _overlayCompleted.Add(key);
            }

            ApplyScreenGridsOnly(card, 0);
        }

        if (_pendingOverlays.Count == 0) return;
        var stale = new List<long>();
        foreach (var key in _pendingOverlays.Keys)
        {
            if (!visibleKeys.Contains(key))
                stale.Add(key);
        }
        foreach (var key in stale)
            _pendingOverlays.Remove(key);
    }

    private static void CollectVisibleOverlayCards(VisualElement ve, List<VisualElement> results)
    {
        if (ve == null) return;

        if (ve.name == "Card" && IsOverlayCardElement(ve) && !results.Contains(ve))
            results.Add(ve);

        for (int i = 0; i < ve.childCount; i++)
            CollectVisibleOverlayCards(ve[i], results);
    }

    private static bool IsOverlayCardElement(VisualElement card)
    {
        try
        {
            for (var p = card.parent; p != null; p = p.parent)
            {
                if (p.name == "Menu") return false;
            }

            return card.layout.width > 300f && card.layout.height > 200f;
        }
        catch { return false; }
    }

    private static void ApplyOverlayCardOnce(VisualElement card)
    {
        float canvasW = LastHeightRatio > 0f ? Screen.width / LastHeightRatio : Screen.width;
        float canvasH = LastHeightRatio > 0f ? Screen.height / LastHeightRatio : Screen.height;

        float topY = 124f;
        try
        {
            float layoutY = card.layout.y;
            if (layoutY > 40f) topY = layoutY;
        }
        catch { }

        float height = canvasH * VerticalFillRatio - topY - 6f;
        if (height < 200f) return;

        if (TryMark(card, ApplyKind.OverlayCard))
        {
            try
            {
                card.style.width = new StyleLength(new Length(canvasW, LengthUnit.Pixel));
                card.style.height = new StyleLength(new Length(height, LengthUnit.Pixel));
                card.style.maxWidth = StyleKeyword.None;
                card.style.maxHeight = StyleKeyword.None;
                card.style.minHeight = StyleKeyword.None;
                card.style.left = new StyleLength(new Length(0f, LengthUnit.Pixel));
                card.style.marginLeft = new StyleLength(new Length(0f, LengthUnit.Pixel));
                card.style.marginRight = new StyleLength(new Length(0f, LengthUnit.Pixel));
            }
            catch { }
        }

        ExpandOverlayCardSubtree(card, 0, canvasW, height);
    }

    private static void ExpandOverlayCardSubtree(VisualElement ve, int depth, float width, float height)
    {
        if (ve == null || depth > 50) return;

        if (LooksLikeCardTemplate(ve) && IsOverlayCardContext(ve))
        {
            ApplyExplicitSize(ve, width, height);
            for (int i = 0; i < ve.childCount; i++)
            {
                var child = ve[i];
                if (child != null) ApplyExplicitSize(child, width, height);
            }
            return;
        }

        for (int i = 0; i < ve.childCount; i++)
            ExpandOverlayCardSubtree(ve[i], depth + 1, width, height);
    }

    private static bool IsModalRoot(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name == "ModalDialog"
            || name == "GenericModalDialog"
            || name == "ExternalNewsDynamicCard";
    }

    private static void CollectVisibleModals(VisualElement ve, List<VisualElement> results)
    {
        if (ve == null) return;

        if (IsModalRoot(ve.name) && IsModalVisible(ve) && !results.Contains(ve))
            results.Add(ve);

        for (int i = 0; i < ve.childCount; i++)
            CollectVisibleModals(ve[i], results);
    }

    private static bool IsModalVisible(VisualElement modal)
    {
        try { return modal.layout.height > 80f && modal.layout.width > 120f; }
        catch { return false; }
    }

    private static void ApplyModalOnce(VisualElement modal)
    {
        try
        {
            if (TryMark(modal, ApplyKind.ModalChrome))
            {
                modal.style.maxHeight = new StyleLength(new Length(100f * VerticalFillRatio, LengthUnit.Percent));
                modal.style.maxWidth = StyleKeyword.None;
            }
        }
        catch { }

        ApplyModalSubtree(modal, 0);
        ExpandModalCardTemplates(modal, 0);
    }

    private static void ApplyModalSubtree(VisualElement ve, int depth)
    {
        if (ve == null || depth > 40) return;

        try
        {
            if (ve.name == "Content" || ve.name == "Overview")
            {
                if (TryMark(ve, ApplyKind.DashboardChrome))
                {
                    ve.style.flexGrow = 1f;
                    ve.style.maxHeight = StyleKeyword.None;
                    ve.style.height = new StyleLength(new Length(100f * VerticalFillRatio, LengthUnit.Percent));
                }
            }
        }
        catch { }

        for (int i = 0; i < ve.childCount; i++)
            ApplyModalSubtree(ve[i], depth + 1);
    }

    private static void ExpandModalCardTemplates(VisualElement ve, int depth)
    {
        if (ve == null || depth > 50) return;

        if (LooksLikeCardTemplate(ve) && IsModalContext(ve))
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
            ExpandModalCardTemplates(ve[i], depth + 1);
    }

    private static bool IsModalContext(VisualElement ve)
    {
        try
        {
            for (var p = ve.parent; p != null; p = p.parent)
            {
                if (IsModalRoot(p.name)) return true;
            }
        }
        catch { }
        return false;
    }

    private void PrunePendingScreens(HashSet<long> visibleKeys)
    {
        if (_pendingScreens.Count == 0) return;
        var stale = new List<long>();
        foreach (var key in _pendingScreens.Keys)
        {
            if (!visibleKeys.Contains(key))
                stale.Add(key);
        }
        foreach (var key in stale)
            _pendingScreens.Remove(key);
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
    private static readonly Dictionary<long, HashSet<ApplyKind>> s_applied = new Dictionary<long, HashSet<ApplyKind>>();
    private static readonly Dictionary<long, (float w, float h)> s_gridRatioCache = new Dictionary<long, (float w, float h)>();

    private enum ApplyKind
    {
        BodyPin,
        DashboardChrome,
        GridChain,
        VerticalExpand,
        ScrollViewFix,
        HorizontalExpand,
        ModalChrome,
        OverlayCard,
        CardWidth,
    }

    private static bool TryMark(VisualElement ve, ApplyKind kind)
    {
        try
        {
            long key = ve.Pointer.ToInt64();
            if (!s_applied.TryGetValue(key, out var kinds))
            {
                kinds = new HashSet<ApplyKind>();
                s_applied[key] = kinds;
            }
            return kinds.Add(kind);
        }
        catch { return true; }
    }

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

    private static void ExpandUltrawideDocuments()
    {
        if (!LastIsUltrawide) return;

        float aspect = (float)Screen.width / Screen.height;
        if (Math.Abs(aspect - RefAspect) < AspectTolerance) return;

        RefreshSkipNames();

        float logicalCanvasW = LastHeightRatio > 0f ? Screen.width / LastHeightRatio : Screen.width;
        float widthThreshold = logicalCanvasW * 0.4f;

        UIDocument[] docs;
        try { docs = GameObject.FindObjectsOfType<UIDocument>(); }
        catch { return; }

        foreach (var doc in docs)
        {
            if (doc == null) continue;
            var root = doc.rootVisualElement;
            if (root == null) continue;

            ExpandElementHorizontal(root, 0, widthThreshold, false);
            ExpandCardTemplates(root, 0);
            ScanForGridLayouts(root, 0, scaleWidth: true, scaleHeight: false, dashboardOnly: false);
        }
    }

    private static void ExpandOverlayDocuments()
    {
        UIDocument[] docs;
        try { docs = GameObject.FindObjectsOfType<UIDocument>(); }
        catch { return; }

        foreach (var doc in docs)
        {
            if (doc == null) continue;
            var root = doc.rootVisualElement;
            if (root == null) continue;
            ExpandOverlayCardTemplates(root, 0);
        }
    }

    private static void CollectVisibleDashboardScreens(VisualElement root, List<VisualElement> results)
    {
        CollectNamedDashboardScreens(root, results);

        var menu = FindDescendantNamed(root, "Menu");
        var body = menu != null ? FindChildNamed(menu, "Body") : null;
        if (body == null) return;

        VisualElement best = null;
        float bestArea = 0f;
        for (int i = 0; i < body.childCount; i++)
        {
            var child = body[i];
            if (child == null) continue;
            try
            {
                float w = child.layout.width;
                float h = child.layout.height;
                if (h < 60f || w < 200f) continue;
                float area = w * h;
                if (area > bestArea)
                {
                    bestArea = area;
                    best = child;
                }
            }
            catch { }
        }

        if (best != null && !results.Contains(best))
            results.Add(best);
    }

    private static void CollectNamedDashboardScreens(VisualElement ve, List<VisualElement> results)
    {
        if (ve == null) return;

        if (IsDashboardScreenCandidate(ve.name) && IsScreenVisible(ve) && !results.Contains(ve))
            results.Add(ve);

        for (int i = 0; i < ve.childCount; i++)
            CollectNamedDashboardScreens(ve[i], results);
    }

    private static bool IsDashboardScreenCandidate(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (name == "PortalScreen") return true;
        if (IsDashboardScreenRoot(name)) return true;
        return name.Equals("Squad", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Recruitment", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsScreenVisible(VisualElement screen)
    {
        try { return screen.layout.height > 60f && screen.layout.width > 200f; }
        catch { return false; }
    }

    private static void ApplyDashboardChromeOnce(VisualElement screen)
    {
        float logicalCanvasH = LastHeightRatio > 0f ? Screen.height / LastHeightRatio : Screen.height;
        ExpandElementVertical(screen, 0, logicalCanvasH * 0.4f, false);
        ApplyScreenChromeSubtree(screen, 0);
    }

    private static void ApplyScreenChromeSubtree(VisualElement ve, int depth)
    {
        if (ve == null || depth > 40) return;

        ApplyDashboardChrome(ve);

        for (int i = 0; i < ve.childCount; i++)
            ApplyScreenChromeSubtree(ve[i], depth + 1);
    }

    private static void ApplyScreenGridsOnly(VisualElement ve, int depth)
    {
        if (ve == null || depth > 40) return;

        for (int i = 0; i < ve.childCount; i++)
        {
            var child = ve[i];
            if (child?.name == "GridLayoutElementContent")
            {
                ExpandGridParentChain(child);
                ScaleGridTiles(child, scaleWidth: false, scaleHeight: true);
            }
            ApplyScreenGridsOnly(child, depth + 1);
        }
    }

    private static void ApplyDashboardScreenOnce(VisualElement screen)
    {
        ApplyDashboardChromeOnce(screen);
        ApplyScreenGridsOnly(screen, 0);
    }

    private static void ApplyScreenSubtree(VisualElement ve, int depth)
    {
        ApplyScreenChromeSubtree(ve, depth);
        ApplyScreenGridsOnly(ve, depth);
    }

    private static VisualElement FindDescendantNamed(VisualElement root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindDescendantNamed(root[i], name);
            if (found != null) return found;
        }
        return null;
    }

    private static void ExpandUIDocuments()
    {
        // Legacy entry point kept for ultrawide polling.
        ExpandUltrawideDocuments();
    }

    /// <summary>
    /// Dashboard pages (Portal, Recruitment, Squad, etc.) live under Menu/Body as *Screen roots.
    /// </summary>
    private static bool IsDashboardScreenRoot(string name)
    {
        if (string.IsNullOrEmpty(name) || !name.EndsWith("Screen", StringComparison.Ordinal))
            return false;
        if (name == "PortalScreen") return false;
        if (name.IndexOf("Tactics", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        if (name.IndexOf("Planner", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        if (name.IndexOf("Pitch", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        if (name.IndexOf("Match", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        return true;
    }

    private static void PinScreenBody(VisualElement body)
    {
        if (!TryMark(body, ApplyKind.BodyPin)) return;

        try
        {
            float offsetY = 0f;
            try { offsetY = body.layout.y; } catch { }
            if (offsetY <= 2f) return;

            body.style.height = StyleKeyword.Null;
            body.style.maxHeight = StyleKeyword.None;
            body.style.bottom = new StyleLength(new Length(0f, LengthUnit.Pixel));
        }
        catch { }
    }

    private static void ApplyDashboardChrome(VisualElement ve)
    {
        try
        {
            if (ve.name == "Body")
            {
                float offsetY = 0f;
                try { offsetY = ve.layout.y; } catch { }
                if (offsetY > 2f)
                {
                    if (TryMark(ve, ApplyKind.BodyPin))
                    {
                        ve.style.height = StyleKeyword.Null;
                        ve.style.maxHeight = StyleKeyword.None;
                        ve.style.bottom = new StyleLength(new Length(0f, LengthUnit.Pixel));
                    }
                    if (TryMark(ve, ApplyKind.DashboardChrome))
                        ve.style.overflow = Overflow.Hidden;
                }
                return;
            }

            if (ve.name == "PortalScreen" || ve.name == "Overview" || ve.name == "Content")
            {
                if (!TryMark(ve, ApplyKind.DashboardChrome)) return;

                ve.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));
                ve.style.maxHeight = StyleKeyword.None;
                ve.style.flexGrow = 1f;
                ve.style.overflow = Overflow.Hidden;
                return;
            }

            if (ve is ScrollView sv)
            {
                bool first = TryMark(ve, ApplyKind.ScrollViewFix);
                if (!first) return;

                sv.verticalScrollerVisibility = ScrollerVisibility.Hidden;
                sv.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                sv.mode = ScrollViewMode.Vertical;
                sv.scrollOffset = Vector2.zero;
                sv.style.overflow = Overflow.Hidden;

                var content = sv.contentContainer;
                if (content != null)
                {
                    content.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));
                    content.style.minHeight = StyleKeyword.None;
                    content.style.flexGrow = 1f;
                }
            }
        }
        catch { }
    }

    private static void ExpandGridParentChain(VisualElement grid)
    {
        try
        {
            for (var p = grid.parent; p != null; p = p.parent)
            {
                if (p.name == "PortalScreen" || IsDashboardScreenRoot(p.name) || IsModalRoot(p.name))
                    break;
                if (p.name == "Card" && IsOverlayCardElement(p))
                    break;

                if (!TryMark(p, ApplyKind.GridChain)) continue;

                p.style.flexGrow = 1f;
                p.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));
                p.style.minHeight = new StyleLength(new Length(100f, LengthUnit.Percent));
                p.style.maxHeight = StyleKeyword.None;
                p.style.overflow = Overflow.Hidden;
            }
        }
        catch { }
    }

    private static void ExpandElementVertical(VisualElement ve, int depth, float threshold, bool skipExpansion)
    {
        if (ve == null || depth > 100) return;

        bool childrenSkip = skipExpansion;

        if (!skipExpansion)
        {
            if (TryMark(ve, ApplyKind.VerticalExpand))
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
            else if (ve.name != null && IsSkipped(ve.name))
            {
                childrenSkip = true;
            }
        }

        for (int i = 0; i < ve.childCount; i++)
            ExpandElementVertical(ve[i], depth + 1, threshold, childrenSkip);
    }

    /// <summary>
    /// Popup overlays (Tactics Planner, dialogs) use Figma card templates with fixed pixel width.
    /// Only expand templates inside the Card overlay slot — not the main-menu Card panel structure.
    /// </summary>
    private static void ExpandOverlayCardTemplates(VisualElement ve, int depth)
    {
        if (ve == null || depth > 50) return;

        if (LooksLikeCardTemplate(ve) && IsOverlayCardContext(ve))
        {
            float canvasW = LastHeightRatio > 0f ? Screen.width / LastHeightRatio : Screen.width;
            float canvasH = LastHeightRatio > 0f ? Screen.height / LastHeightRatio : Screen.height;
            float height = canvasH * VerticalFillRatio - 130f;
            if (height < 200f) height = canvasH * 0.85f;
            ApplyExplicitSize(ve, canvasW, height);
            for (int i = 0; i < ve.childCount; i++)
            {
                var child = ve[i];
                if (child != null) ApplyExplicitSize(child, canvasW, height);
            }
            return;
        }

        for (int i = 0; i < ve.childCount; i++)
            ExpandOverlayCardTemplates(ve[i], depth + 1);
    }

    private static bool IsOverlayCardContext(VisualElement ve)
    {
        try
        {
            for (var p = ve.parent; p != null; p = p.parent)
            {
                if (p.name == "Card")
                {
                    try { return p.layout.width > 100f && p.layout.height > 100f; }
                    catch { return true; }
                }
                if (p.name == "Menu") return false;
            }
        }
        catch { }
        return false;
    }

    private static VisualElement FindChildNamed(VisualElement parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent[i];
            if (c != null && c.name == name) return c;
        }
        return null;
    }

    private static void ExpandElementHorizontal(VisualElement ve, int depth, float threshold, bool skipExpansion)
    {
        if (ve == null || depth > 100) return;

        bool childrenSkip = skipExpansion;

        if (!skipExpansion)
        {
            if (depth <= 1)
            {
                if (TryMark(ve, ApplyKind.HorizontalExpand))
                {
                    ForceFullWidth(ve);
                    if (depth == 0)
                        ve.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));
                }

                if (ve.name != null && IsSkipped(ve.name))
                    childrenSkip = true;
            }
            else if (ve.name != null && IsSkipped(ve.name))
            {
                childrenSkip = true;
            }
            else if (TryMark(ve, ApplyKind.HorizontalExpand))
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
        ApplyExplicitSize(ve, pixelWidth, -1f);
    }

    private static void ApplyExplicitSize(VisualElement ve, float pixelWidth, float pixelHeight)
    {
        if (!TryMark(ve, ApplyKind.CardWidth)) return;

        try
        {
            ve.style.paddingLeft = new StyleLength(new Length(0f, LengthUnit.Pixel));
            ve.style.paddingRight = new StyleLength(new Length(0f, LengthUnit.Pixel));
            ve.style.marginLeft = new StyleLength(new Length(0f, LengthUnit.Pixel));
            ve.style.marginRight = new StyleLength(new Length(0f, LengthUnit.Pixel));
            ve.style.width = new StyleLength(new Length(pixelWidth, LengthUnit.Pixel));
            ve.style.maxWidth = StyleKeyword.None;
            if (pixelHeight > 0f)
            {
                ve.style.height = new StyleLength(new Length(pixelHeight, LengthUnit.Pixel));
                ve.style.maxHeight = StyleKeyword.None;
                ve.style.minHeight = StyleKeyword.None;
            }
            ve.style.overflow = Overflow.Visible;
        }
        catch { }
    }

    private static void ScanForGridLayouts(VisualElement ve, int depth, bool scaleWidth, bool scaleHeight, bool dashboardOnly)
    {
        if (ve == null || depth > 35) return;
        if (ve.name == "GridLayoutElementContent")
        {
            if (!dashboardOnly || IsDashboardGrid(ve))
                ScaleGridTiles(ve, scaleWidth, scaleHeight);
        }
        for (int i = 0; i < ve.childCount; i++)
            ScanForGridLayouts(ve[i], depth + 1, scaleWidth, scaleHeight, dashboardOnly);
    }

    private static bool IsDashboardGrid(VisualElement grid)
    {
        try
        {
            for (var p = grid.parent; p != null; p = p.parent)
            {
                if (IsDashboardScreenRoot(p.name) || p.name == "PortalScreen" || IsModalRoot(p.name)) return true;
                if (p.name == "Card" || p.name == "ModalDialog") return false;
            }
        }
        catch { }
        return false;
    }

    private static void ScaleGridTiles(VisualElement container, bool scaleWidth, bool scaleHeight)
    {
        if (LastRefResWidth <= 0f || LastRefResHeight <= 0f || LastHeightRatio <= 0f) return;

        float logicalCanvasW = Screen.width / LastHeightRatio;
        float logicalCanvasH = Screen.height / LastHeightRatio;
        float ratioW = logicalCanvasW / LastRefResWidth;
        float ratioH = logicalCanvasH / LastRefResHeight;

        if (scaleWidth && ratioW < 1.02f) scaleWidth = false;

        if (scaleHeight)
            ratioH = ComputeGridHeightRatio(container, ratioH);

        if (scaleHeight && ratioH < 1.02f) scaleHeight = false;
        if (!scaleWidth && !scaleHeight) return;

        long gridKey = container.Pointer.ToInt64();

        if (s_gridRatioCache.TryGetValue(gridKey, out var cached)
            && (!scaleWidth || Math.Abs(cached.w - ratioW) < 0.005f)
            && (!scaleHeight || Math.Abs(cached.h - ratioH) < 0.005f))
            return;

        bool anyApplied = false;
        bool sawPixelTiles = false;

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
                    {
                        float targetW = baseW * ratioW;
                        if (Math.Abs(ws.value.value - targetW) > 1f)
                        {
                            child.style.width = new StyleLength(new Length(targetW, LengthUnit.Pixel));
                            anyApplied = true;
                        }
                    }
                }

                var ls = child.style.left;
                if (scaleWidth
                    && ls.keyword == StyleKeyword.Undefined
                    && ls.value.unit == LengthUnit.Pixel
                    && ls.value.value > 0f)
                {
                    float baseL = s_tileOriginalLefts.TryGetValue(key, out float sl) ? sl : ls.value.value;
                    if (!s_tileOriginalLefts.ContainsKey(key)) s_tileOriginalLefts[key] = ls.value.value;
                    float targetL = baseL * ratioW;
                    if (Math.Abs(ls.value.value - targetL) > 1f)
                    {
                        child.style.left = new StyleLength(new Length(targetL, LengthUnit.Pixel));
                        anyApplied = true;
                    }
                }

                var hs = child.style.height;
                if (scaleHeight
                    && hs.keyword == StyleKeyword.Undefined
                    && hs.value.unit == LengthUnit.Pixel
                    && hs.value.value > 0f)
                {
                    sawPixelTiles = true;
                    float baseH = s_tileOriginalHeights.TryGetValue(key, out float sh) ? sh : hs.value.value;
                    if (!s_tileOriginalHeights.ContainsKey(key)) s_tileOriginalHeights[key] = hs.value.value;
                    float targetH = baseH * ratioH;
                    if (Math.Abs(hs.value.value - targetH) > 1f)
                    {
                        child.style.height = new StyleLength(new Length(targetH, LengthUnit.Pixel));
                        anyApplied = true;
                    }
                }

                var ts = child.style.top;
                if (scaleHeight
                    && ts.keyword == StyleKeyword.Undefined
                    && ts.value.unit == LengthUnit.Pixel
                    && ts.value.value > 0f)
                {
                    float baseT = s_tileOriginalTops.TryGetValue(key, out float st) ? st : ts.value.value;
                    if (!s_tileOriginalTops.ContainsKey(key)) s_tileOriginalTops[key] = ts.value.value;
                    float targetT = baseT * ratioH;
                    if (Math.Abs(ts.value.value - targetT) > 1f)
                    {
                        child.style.top = new StyleLength(new Length(targetT, LengthUnit.Pixel));
                        anyApplied = true;
                    }
                }
            }
            catch { }
        }

        if (anyApplied || sawPixelTiles)
            s_gridRatioCache[gridKey] = (ratioW, ratioH);
    }

    private static float ApplyVerticalFill(float ratio)
    {
        if (ratio < 1.02f) return ratio;
        return ratio * VerticalFillRatio;
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

            float targetH = GetDashboardContentHeight(container);
            if (targetH > containerH + 4f)
                containerH = targetH;

            if (containerH <= 1f) return aspectRatio;

            float designBottom = 0f;
            for (int i = 0; i < container.childCount; i++)
            {
                var child = container[i];
                if (child == null) continue;

                float top = 0f, height = 0f;
                long tileKey = child.Pointer.ToInt64();
                if (s_tileOriginalTops.TryGetValue(tileKey, out float storedTop))
                    top = storedTop;
                else
                {
                    try
                    {
                        var ts = child.style.top;
                        if (ts.keyword == StyleKeyword.Undefined && ts.value.unit == LengthUnit.Pixel)
                            top = ts.value.value;
                        else
                            top = child.layout.y;
                    }
                    catch { }
                }

                if (s_tileOriginalHeights.TryGetValue(tileKey, out float storedH))
                    height = storedH;
                else
                {
                    try
                    {
                        var hs = child.style.height;
                        if (hs.keyword == StyleKeyword.Undefined && hs.value.unit == LengthUnit.Pixel)
                            height = hs.value.value;
                        else
                            height = child.layout.height;
                    }
                    catch { }
                }

                if (height > 1f)
                    designBottom = Math.Max(designBottom, top + height);
            }

            if (designBottom > 1f && containerH > designBottom + 4f)
                return ApplyVerticalFill(Math.Max(aspectRatio, containerH / designBottom));
        }
        catch { }

        return ApplyVerticalFill(aspectRatio);
    }

    private static float GetDashboardContentHeight(VisualElement grid)
    {
        try
        {
            for (var p = grid.parent; p != null; p = p.parent)
            {
                if (p.name == "Content" || p.name == "Overview")
                {
                    float h = p.layout.height;
                    if (h > 1f) return h;
                }
                if (p.name != null && p.name.EndsWith("Screen", StringComparison.Ordinal))
                {
                    float h = p.layout.height;
                    if (h > 1f) return h;
                }
            }
        }
        catch { }

        return 0f;
    }

    private static void ForceFullWidth(VisualElement ve)
    {
        ve.style.width = new StyleLength(new Length(100f, LengthUnit.Percent));
        ve.style.maxWidth = StyleKeyword.None;
        ve.style.marginLeft = new StyleLength(new Length(0f, LengthUnit.Pixel));
        ve.style.marginRight = new StyleLength(new Length(0f, LengthUnit.Pixel));
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
}
