using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Floating HP bar that renders in a Screen Space - Overlay canvas (always on top of 3D geometry).
/// Uses one shared canvas for all instances. Call Init() after spawning the unit.
/// </summary>
public class FloatingHealthBar : MonoBehaviour
{
    private Unit _unit;
    private Image _fill;
    private TextMeshProUGUI _label;
    private Image _manaFill;
    private TextMeshProUGUI _manaLabel;
    private RectTransform _affixRow;
    private RectTransform _barRect;
    private RectTransform _statusRow;
    private string _lastStatusKey = "__init__";

    private const float WorldYOffset = 1.6f;   // world units above the unit's pivot
    private const float BarWidth = 220f;
    private const float BarHeight = 32f;
    private const float BarHeightWithMana = 44f;

    // ── Shared overlay canvas (one per session) ───────────────────────────────
    private static Canvas s_Canvas;
    private static RectTransform s_CanvasRect;

    // ── White sprite for Image.Type.Filled ────────────────────────────────────
    private static Sprite _whiteSprite;
    private static Sprite WhiteSprite
    {
        get
        {
            if (_whiteSprite != null) return _whiteSprite;
            var tex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            return _whiteSprite;
        }
    }

    // ── Optional custom status-icon sprites (loaded from Resources once) ──────
    private static StatusIconConfig s_IconConfig;
    private static bool s_IconConfigLoaded;
    private static StatusIconConfig IconConfig
    {
        get
        {
            if (!s_IconConfigLoaded)
            {
                s_IconConfig = Resources.Load<StatusIconConfig>("StatusIconConfig");
                s_IconConfigLoaded = true;
            }
            return s_IconConfig;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    public void Init(Unit unit, bool isPlayer)
    {
        _unit = unit;
        EnsureSharedCanvas();

        bool hasMana = unit.maxMana > 0;
        float currentBarHeight = hasMana ? BarHeightWithMana : BarHeight;

        // Container panel
        var container = new GameObject($"HPBar_{unit.unitName}");
        container.transform.SetParent(s_Canvas.transform, false);
        _barRect = container.AddComponent<RectTransform>();
        _barRect.sizeDelta = new Vector2(BarWidth, currentBarHeight);
        _barRect.pivot = new Vector2(0.5f, 0f);
        _barRect.anchorMin = new Vector2(0.5f, 0.5f);
        _barRect.anchorMax = new Vector2(0.5f, 0.5f);

        // Dark background
        MakeImage(container.transform, "BG",
            new Color(0.05f, 0.05f, 0.05f, 0.85f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        float hpBottomAnchor = hasMana ? 0.35f : 0f;
        float hpTargetMinY = hpBottomAnchor + (hasMana ? 0.05f : 0.15f);
        float hpTargetMaxY = hasMana ? 0.95f : 0.85f;

        // Red danger underlay
        MakeImage(container.transform, "DangerBG",
            new Color(0.55f, 0.10f, 0.10f, 0.70f),
            new Vector2(0f, hpTargetMinY), new Vector2(1f, hpTargetMaxY),
            new Vector2(2f, 0f), new Vector2(-2f, 0f));

        // Coloured fill
        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(container.transform, false);
        _fill = fillGO.AddComponent<Image>();
        _fill.sprite = WhiteSprite;
        _fill.color = isPlayer
            ? new Color(0.31f, 0.66f, 0.38f)   // green (matches hero card in GameUi.uss)
            : new Color(0.75f, 0.19f, 0.19f);   // red (matches --hp)
        _fill.type = Image.Type.Filled;
        _fill.fillMethod = Image.FillMethod.Horizontal;
        _fill.fillOrigin = 0;
        _fill.fillAmount = 1f;
        SetRect(_fill.rectTransform,
            new Vector2(0f, hpTargetMinY), new Vector2(1f, hpTargetMaxY),
            new Vector2(2f, 0f), new Vector2(-2f, 0f));

        // HP label
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(container.transform, false);
        _label = labelGO.AddComponent<TextMeshProUGUI>();
        _label.fontSize = 15f;
        _label.alignment = TextAlignmentOptions.Center;
        _label.color = Color.white;
        _label.fontStyle = FontStyles.Bold;
        SetRect(_label.rectTransform,
            new Vector2(0f, hpBottomAnchor), Vector2.one, Vector2.zero, Vector2.zero);

        if (hasMana)
        {
            // Mana dark underlay
            MakeImage(container.transform, "ManaDangerBG",
                new Color(0.10f, 0.10f, 0.20f, 0.70f),
                new Vector2(0f, 0.05f), new Vector2(1f, hpBottomAnchor - 0.05f),
                new Vector2(2f, 0f), new Vector2(-2f, 0f));

            // Mana fill
            var manaFillGO = new GameObject("ManaFill");
            manaFillGO.transform.SetParent(container.transform, false);
            _manaFill = manaFillGO.AddComponent<Image>();
            _manaFill.sprite = WhiteSprite;
            _manaFill.color = new Color(0.27f, 0.43f, 0.84f); // matches --mp
            _manaFill.type = Image.Type.Filled;
            _manaFill.fillMethod = Image.FillMethod.Horizontal;
            _manaFill.fillOrigin = 0;
            _manaFill.fillAmount = 1f;
            SetRect(_manaFill.rectTransform,
                new Vector2(0f, 0.05f), new Vector2(1f, hpBottomAnchor - 0.05f),
                new Vector2(2f, 0f), new Vector2(-2f, 0f));

            // Mana label (optional, maybe no text for mana or smaller text)
            var manaLabelGO = new GameObject("ManaLabel");
            manaLabelGO.transform.SetParent(container.transform, false);
            _manaLabel = manaLabelGO.AddComponent<TextMeshProUGUI>();
            _manaLabel.fontSize = 12f;
            _manaLabel.alignment = TextAlignmentOptions.Center;
            _manaLabel.color = Color.white;
            _manaLabel.fontStyle = FontStyles.Bold;
            SetRect(_manaLabel.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, hpBottomAnchor), Vector2.zero, Vector2.zero);
        }

        // Affix row — individual colored labels per affix, hidden until SetAffixes is called
        var affixRowGO = new GameObject("AffixRow");
        affixRowGO.transform.SetParent(container.transform, false);
        _affixRow = affixRowGO.AddComponent<RectTransform>();
        SetRect(_affixRow,
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, -40f), new Vector2(0f, -2f));
        var layout = affixRowGO.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 2f;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.padding = new RectOffset(0, 0, 0, 0);
        var csf = affixRowGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        affixRowGO.SetActive(false);

        // Status-effect icon row — sits to the left of the bar
        var statusRowGO = new GameObject("StatusRow");
        statusRowGO.transform.SetParent(container.transform, false);
        _statusRow = statusRowGO.AddComponent<RectTransform>();
        _statusRow.anchorMin = new Vector2(0f, 0f);
        _statusRow.anchorMax = new Vector2(0f, 1f);
        _statusRow.pivot = new Vector2(1f, 0.5f);
        _statusRow.offsetMin = new Vector2(-140f, 0f);
        _statusRow.offsetMax = new Vector2(-4f, 0f);
        var statusLayout = statusRowGO.AddComponent<HorizontalLayoutGroup>();
        statusLayout.childAlignment = TextAnchor.MiddleRight;
        statusLayout.spacing = 4f;
        statusLayout.childForceExpandWidth = false;
        statusLayout.childForceExpandHeight = false;
        statusLayout.padding = new RectOffset(0, 0, 0, 0);
        var statusCsf = statusRowGO.AddComponent<ContentSizeFitter>();
        statusCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        statusRowGO.SetActive(false);

        UpdateBar();
    }

    private void OnDestroy()
    {
        // The container lives in the shared canvas (not under this GameObject), so we
        // must destroy it explicitly when the unit is cleaned up between encounters.
        if (_barRect != null) Destroy(_barRect.gameObject);
    }

    private void LateUpdate()
    {
        if (_unit == null || _barRect == null) return;

        if (_unit.isDead)
        {
            _barRect.gameObject.SetActive(false);
            return;
        }

        _barRect.gameObject.SetActive(true);
        UpdateBar();
        RefreshStatusIcons();
        PositionBar();
    }

    public void RefreshStatusIcons()
    {
        if (_unit == null || _statusRow == null) return;

        var effects = _unit.activeStatusEffects;

        // Build a cheap change-detection key: "type:duration,type:duration,..."
        string key = "";
        foreach (var e in effects) key += (int)e.type + ":" + e.duration + ",";
        if (key == _lastStatusKey) return;
        _lastStatusKey = key;

        foreach (Transform child in _statusRow) Destroy(child.gameObject);

        if (effects == null || effects.Count == 0)
        {
            _statusRow.gameObject.SetActive(false);
            return;
        }

        foreach (var effect in effects)
            MakeStatusIcon(effect);

        _statusRow.gameObject.SetActive(true);
    }

    private void MakeStatusIcon(StatusEffect effect)
    {
        const float Size = 30f;

        var go = new GameObject($"Status_{effect.type}");
        go.transform.SetParent(_statusRow, false);

        Sprite customSprite = IconConfig != null ? IconConfig.GetIcon(effect.type) : null;
        bool hasCustomSprite = customSprite != null;

        // Background: use custom sprite (white tint) or coloured fallback square
        var img = go.AddComponent<Image>();
        img.sprite = hasCustomSprite ? customSprite : WhiteSprite;
        img.color = hasCustomSprite ? Color.white : StatusIconColor(effect.type);
        img.preserveAspect = hasCustomSprite;
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = Size;
        le.preferredHeight = Size;
        le.minWidth = Size;
        le.minHeight = Size;

        // Abbreviated letter — only shown when no custom sprite is assigned
        var lblGO = new GameObject("Char");
        lblGO.transform.SetParent(go.transform, false);
        lblGO.SetActive(!hasCustomSprite);
        var lbl = lblGO.AddComponent<TextMeshProUGUI>();
        lbl.text = StatusIconChar(effect.type);
        lbl.fontSize = 17f;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.color = Color.white;
        lbl.fontStyle = FontStyles.Bold;
        var lblRt = lbl.rectTransform;
        lblRt.anchorMin = Vector2.zero;
        lblRt.anchorMax = Vector2.one;
        lblRt.offsetMin = Vector2.zero;
        lblRt.offsetMax = Vector2.zero;

        // Turn-count badge in the bottom-right corner (always visible)
        var badgeGO = new GameObject("Turns");
        badgeGO.transform.SetParent(go.transform, false);
        var badge = badgeGO.AddComponent<TextMeshProUGUI>();
        badge.text = effect.duration.ToString();
        badge.fontSize = 12f;
        badge.alignment = TextAlignmentOptions.BottomRight;
        badge.color = new Color(1f, 1f, 0.7f, 1f);
        var bRt = badge.rectTransform;
        bRt.anchorMin = Vector2.zero;
        bRt.anchorMax = Vector2.one;
        bRt.offsetMin = new Vector2(0f, 2f);
        bRt.offsetMax = new Vector2(-2f, 0f);
    }

    private static Color StatusIconColor(StatusEffectType type) => type switch
    {
        StatusEffectType.Burn => new Color(1.00f, 0.35f, 0.10f),  // red-orange
        StatusEffectType.Stun => new Color(0.50f, 0.55f, 1.00f),  // periwinkle
        StatusEffectType.Weaken => new Color(0.65f, 0.15f, 0.85f),  // purple
        StatusEffectType.Fortify => new Color(0.15f, 0.70f, 0.85f),  // teal
        _ => new Color(0.60f, 0.60f, 0.60f),  // grey
    };

    private static string StatusIconChar(StatusEffectType type) => type switch
    {
        StatusEffectType.Burn => "\u2605",  // ★  (flame-like star)
        StatusEffectType.Stun => "\u26a1",  // ⚡
        StatusEffectType.Weaken => "\u2193",  // ↓
        StatusEffectType.Fortify => "\u2191",  // ↑
        _ => "?",
    };

    public void SetAffixes(IReadOnlyList<EnemyAffix> affixes)
    {
        if (_affixRow == null || affixes == null || affixes.Count == 0) return;

        // Destroy any previously-created chips
        foreach (Transform child in _affixRow) Destroy(child.gameObject);

        for (int i = 0; i < affixes.Count; i++)
        {
            MakeAffixChip(affixes[i].name, AffixColor(affixes[i].type));
        }

        _affixRow.gameObject.SetActive(true);
    }

    private void MakeAffixChip(string text, Color color)
    {
        var go = new GameObject(text);
        go.transform.SetParent(_affixRow, false);
        var lbl = go.AddComponent<TextMeshProUGUI>();
        lbl.text = text;
        lbl.fontSize = 22f;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.color = color;
        lbl.fontStyle = FontStyles.Bold;

        var csf = go.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static Color AffixColor(EnemyAffixType type) => type switch
    {
        EnemyAffixType.Armored => new Color(0.49f, 0.78f, 0.89f),  // steel blue
        EnemyAffixType.Berserker => new Color(1.00f, 0.27f, 0.27f),  // red
        EnemyAffixType.Swift => new Color(0.66f, 1.00f, 0.47f),  // lime green
        EnemyAffixType.Bulwark => new Color(0.75f, 0.63f, 0.38f),  // bronze
        EnemyAffixType.Regenerating => new Color(0.27f, 0.87f, 0.53f),  // teal green
        EnemyAffixType.Thorns => new Color(0.80f, 0.33f, 1.00f),  // purple
        EnemyAffixType.Vampiric => new Color(1.00f, 0.40f, 0.60f),  // crimson pink
        EnemyAffixType.Enraged => new Color(1.00f, 0.53f, 0.00f),  // orange
        _ => new Color(1.00f, 0.87f, 0.27f),  // gold
    };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void UpdateBar()
    {
        if (_fill != null)
        {
            float ratio = Mathf.Clamp01((float)_unit.currentHealth / Mathf.Max(1, _unit.maxHealth));
            _fill.fillAmount = ratio;
        }
        if (_label != null) _label.text = $"{_unit.currentHealth}/{_unit.maxHealth}";

        if (_manaFill != null)
        {
            float manaRatio = Mathf.Clamp01((float)_unit.currentMana / Mathf.Max(1, _unit.maxMana));
            _manaFill.fillAmount = manaRatio;
        }
        if (_manaLabel != null) _manaLabel.text = $"{_unit.currentMana}/{_unit.maxMana}";
    }

    private void PositionBar()
    {
        Camera cam = Camera.main;
        if (cam == null || s_CanvasRect == null) return;

        Vector3 worldPos = transform.position + Vector3.up * WorldYOffset;
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        // Hide if behind the camera
        if (screenPos.z < 0f)
        {
            _barRect.gameObject.SetActive(false);
            return;
        }

        // Clamp so bars don't overlap the run-rewards strip at the top
        screenPos.y = Mathf.Min(screenPos.y, Screen.height * 0.70f);

        // Screen → overlay-canvas local position (camera = null for SS-Overlay)
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            s_CanvasRect, new Vector2(screenPos.x, screenPos.y),
            null, out Vector2 canvasPos);
        _barRect.anchoredPosition = canvasPos;
    }

    /// <summary>
    /// Immediately destroys every HP bar container in the shared canvas.
    /// Call this at the start of a new encounter (before SetupBattle) so stale
    /// bars from the previous round are gone before new ones are created.
    /// </summary>
    public static void ClearAll()
    {
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
        // Below the UI Toolkit HUD panel (sorting order 0) so overlays and menus draw over world-space bars.
        s_Canvas.sortingOrder = -10;
        go.AddComponent<GraphicRaycaster>();
        s_CanvasRect = s_Canvas.GetComponent<RectTransform>();
    }

    private static void MakeImage(Transform parent, string name, Color color,
        Vector2 ancMin, Vector2 ancMax, Vector2 offMin, Vector2 offMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        SetRect(img.rectTransform, ancMin, ancMax, offMin, offMax);
    }

    private static void SetRect(RectTransform rt,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }
}


