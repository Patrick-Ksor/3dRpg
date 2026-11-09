using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Per-unit driver for the world-following nameplate. The plate itself is a UI Toolkit element
/// (see <see cref="Nameplates"/>); this component feeds it the unit's world position every frame.
/// The class also owns the shared uGUI canvas used by <see cref="DamagePopSpawner"/>.
/// </summary>
public class FloatingHealthBar : MonoBehaviour
{
    private const float AnchorHeight = 1.9f;   // world units above the unit's pivot for the plate

    private Unit _unit;
    private Nameplates.Plate _plate;

    // ── Shared overlay canvas (damage pops only) ──────────────────────────────
    private static Canvas s_Canvas;
    private static RectTransform s_CanvasRect;

    public void Init(Unit unit, bool isPlayer)
    {
        _unit = unit;
        EnsureSharedCanvas();
        _plate = Nameplates.Create(unit, isPlayer);
    }

    private void OnDestroy()
    {
        _plate?.Destroy();
    }

    private void LateUpdate()
    {
        if (_unit == null || _plate == null) return;
        Vector3 pos = transform.position;
        _plate.Refresh(Camera.main, pos + Vector3.up * AnchorHeight, pos);
    }

    /// <summary>Kept for API compatibility; status chips refresh themselves every frame.</summary>
    public void RefreshStatusIcons() { }

    public void SetAffixes(IReadOnlyList<EnemyAffix> affixes) => _plate?.SetAffixes(affixes);

    /// <summary>Shows or hides every nameplate and pop (used while full-screen overlays are open).</summary>
    public static void SetCanvasVisible(bool visible)
    {
        Nameplates.SetVisible(visible);
        if (s_Canvas != null) s_Canvas.enabled = visible;
    }

    /// <summary>Removes every nameplate and stray damage pop. Call at the start of a new encounter.</summary>
    public static void ClearAll()
    {
        Nameplates.ClearAll();
        if (s_Canvas == null) return;
        foreach (Transform child in s_Canvas.transform)
            Destroy(child.gameObject);
    }

    /// <summary>Returns the shared overlay canvas, creating it if needed. Used by DamagePopSpawner.</summary>
    public static Canvas GetSharedCanvas()
    {
        EnsureSharedCanvas();
        return s_Canvas;
    }

    private static void EnsureSharedCanvas()
    {
        if (s_Canvas != null) return;
        var go = new GameObject("FloatingHPCanvas");
        DontDestroyOnLoad(go);
        s_Canvas = go.AddComponent<Canvas>();
        s_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Below the UI Toolkit HUD panel (sorting order 0) so overlays and menus draw over pops.
        s_Canvas.sortingOrder = -10;
        go.AddComponent<GraphicRaycaster>();
        s_CanvasRect = s_Canvas.GetComponent<RectTransform>();
    }
}
