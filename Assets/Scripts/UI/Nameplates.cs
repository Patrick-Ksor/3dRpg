using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// World-following nameplates drawn in the UI Toolkit HUD (layer: "nameplate-layer" in GameUi.uxml,
/// styles: np-* classes in GameUi.uss). Enemy plates are clickable to choose the attack target.
/// </summary>
public static class Nameplates
{
    private static VisualElement s_Layer;
    private static Action<Unit> s_OnEnemyClicked;
    private static readonly List<Plate> s_Plates = new List<Plate>();
    private static Unit s_Target;

    private static StatusIconConfig s_IconConfig;
    private static bool s_IconConfigLoaded;

    public static void SetLayer(VisualElement layer, Action<Unit> onEnemyClicked)
    {
        s_Layer = layer;
        s_OnEnemyClicked = onEnemyClicked;
        s_Plates.Clear();
        s_Layer?.schedule.Execute(SeparatePlates).Every(16);
    }

    /// <summary>
    /// Plates follow their unit's screen x. When units stand close together the plates would overlap, so spread
    /// them apart left-to-right while keeping the group centred on where the units are.
    /// </summary>
    private static void SeparatePlates()
    {
        var visible = new List<Plate>();
        foreach (Plate p in s_Plates) if (p.IsShown) visible.Add(p);
        if (visible.Count == 0) return;

        visible.Sort((a, b) => a.DesiredX.CompareTo(b.DesiredX));

        const float gap = 8f;
        var x = new float[visible.Count];
        for (int i = 0; i < visible.Count; i++)
        {
            x[i] = visible[i].DesiredX;
            if (i > 0)
            {
                float minX = x[i - 1] + visible[i - 1].Width * 0.5f + visible[i].Width * 0.5f + gap;
                if (x[i] < minX) x[i] = minX;
            }
        }

        // Re-centre pushed clusters so plates drift both ways, not all to the right.
        int start = 0;
        while (start < visible.Count)
        {
            int end = start;
            while (end + 1 < visible.Count && x[end + 1] - x[end] <= visible[end].Width * 0.5f + visible[end + 1].Width * 0.5f + gap + 0.5f) end++;
            float shift = 0f;
            for (int i = start; i <= end; i++) shift += x[i] - visible[i].DesiredX;
            shift /= (end - start + 1);
            for (int i = start; i <= end; i++) x[i] -= shift;
            start = end + 1;
        }

        // Final pass: re-centring one cluster can slide it under its neighbour, so enforce the spacing between
        // all plates again (pushing right), then keep the whole row on screen (pulling back left).
        float panelWidth = s_Layer != null && s_Layer.resolvedStyle.width > 100f ? s_Layer.resolvedStyle.width : 1280f;
        for (int i = 0; i < visible.Count; i++)
        {
            x[i] = Mathf.Max(x[i], visible[i].Width * 0.5f + gap);
            if (i > 0)
                x[i] = Mathf.Max(x[i], x[i - 1] + visible[i - 1].Width * 0.5f + visible[i].Width * 0.5f + gap);
        }
        int last = visible.Count - 1;
        float maxX = panelWidth - visible[last].Width * 0.5f - gap;
        if (x[last] > maxX)
        {
            x[last] = maxX;
            for (int i = last - 1; i >= 0; i--)
                x[i] = Mathf.Min(x[i], x[i + 1] - visible[i].Width * 0.5f - visible[i + 1].Width * 0.5f - gap);
        }

        for (int i = 0; i < visible.Count; i++) visible[i].SetLeft(x[i]);
    }

    public static Plate Create(Unit unit, bool isPlayer)
    {
        if (s_Layer == null || s_Layer.panel == null || unit == null) return null;
        var plate = new Plate(unit, isPlayer, s_OnEnemyClicked, s_Layer);
        s_Plates.Add(plate);
        plate.SetTargeted(unit == s_Target);
        return plate;
    }

    /// <summary>Highlights one enemy plate (gold border + ground ring). Pass null to clear.</summary>
    public static void SetTarget(Unit unit)
    {
        s_Target = unit;
        foreach (Plate p in s_Plates) p.SetTargeted(p.Unit == unit && unit != null);
    }

    /// <summary>Pulses every living enemy plate to say "click one" while a skill waits for its target.</summary>
    public static void SetPicking(bool picking)
    {
        foreach (Plate p in s_Plates) p.SetPicking(picking);
    }

    public static void SetVisible(bool visible)
    {
        if (s_Layer != null) s_Layer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public static void ClearAll()
    {
        // Plate.Destroy removes itself from s_Plates, so iterate over a copy.
        foreach (Plate p in s_Plates.ToArray()) p.Destroy();
        s_Plates.Clear();
        s_Target = null;
    }

    internal static void Remove(Plate plate) => s_Plates.Remove(plate);

    internal static StatusIconConfig IconConfig
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

    public class Plate
    {
        public readonly Unit Unit;
        private readonly bool _isPlayer;
        private readonly VisualElement _root;
        private readonly VisualElement _ring;
        private readonly VisualElement _hpFill;
        private readonly Label _hpText;
        private readonly VisualElement _mpFill;
        private readonly VisualElement _statusRow;
        private readonly VisualElement _affixRow;
        private string _lastStatusKey = "__init__";

        public float DesiredX { get; private set; }
        public bool IsShown { get; private set; }
        public float Width => 224f;
        public void SetLeft(float x) => _root.style.left = x;

        public Plate(Unit unit, bool isPlayer, Action<Unit> onClick, VisualElement layer)
        {
            Unit = unit;
            _isPlayer = isPlayer;

            _ring = new VisualElement { pickingMode = PickingMode.Ignore };
            _ring.AddToClassList("np-ring");
            layer.Add(_ring);

            _root = new VisualElement();
            _root.AddToClassList("np");
            _root.AddToClassList(isPlayer ? "np--player" : "np--enemy");
            layer.Add(_root);

            var nameLabel = new Label(BattleUIManager.PrettyName(unit.unitName)) { pickingMode = PickingMode.Ignore };
            nameLabel.AddToClassList("np-name");
            _root.Add(nameLabel);

            var hp = new VisualElement { pickingMode = PickingMode.Ignore };
            hp.AddToClassList("np-hp");
            _hpFill = new VisualElement { pickingMode = PickingMode.Ignore };
            _hpFill.AddToClassList("np-hp-fill");
            hp.Add(_hpFill);
            _hpText = new Label { pickingMode = PickingMode.Ignore };
            _hpText.AddToClassList("np-hp-text");
            hp.Add(_hpText);
            _root.Add(hp);

            if (unit.maxMana > 0)
            {
                var mp = new VisualElement { pickingMode = PickingMode.Ignore };
                mp.AddToClassList("np-mp");
                _mpFill = new VisualElement { pickingMode = PickingMode.Ignore };
                _mpFill.AddToClassList("np-mp-fill");
                mp.Add(_mpFill);
                _root.Add(mp);
            }

            _statusRow = new VisualElement { pickingMode = PickingMode.Ignore };
            _statusRow.AddToClassList("np-status");
            _root.Add(_statusRow);

            _affixRow = new VisualElement { pickingMode = PickingMode.Ignore };
            _affixRow.AddToClassList("np-affixes");
            _root.Add(_affixRow);

            if (!isPlayer && onClick != null)
                _root.RegisterCallback<ClickEvent>(_ => { if (!unit.isDead) onClick(unit); });
        }

        public void SetPicking(bool picking)
        {
            if (!_isPlayer) _root.EnableInClassList("np--pick", picking);
        }

        public void SetTargeted(bool targeted)
        {
            _root.EnableInClassList("np--target", targeted);
            _ring.EnableInClassList("np-ring--on", targeted);
        }

        public void SetAffixes(IReadOnlyList<EnemyAffix> affixes)
        {
            _affixRow.Clear();
            if (affixes == null) return;
            foreach (EnemyAffix a in affixes)
            {
                var lbl = new Label(a.name) { pickingMode = PickingMode.Ignore };
                lbl.AddToClassList("np-affix");
                lbl.style.color = AffixColor(a.type);
                _affixRow.Add(lbl);
            }
        }

        public void Refresh(Camera cam, Vector3 anchorWorld, Vector3 feetWorld)
        {
            if (_root.panel == null || cam == null) return;

            bool show = !Unit.isDead && cam.WorldToScreenPoint(anchorWorld).z > 0f;
            _root.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            IsShown = show;
            if (!show) { _ring.style.display = DisplayStyle.None; return; }
            _ring.style.display = DisplayStyle.Flex;

            float hpRatio = Mathf.Clamp01((float)Unit.currentHealth / Mathf.Max(1, Unit.maxHealth));
            _hpFill.style.width = Length.Percent(hpRatio * 100f);
            _hpText.text = $"{Unit.currentHealth}/{Unit.maxHealth}";
            if (_mpFill != null)
                _mpFill.style.width = Length.Percent(Mathf.Clamp01((float)Unit.currentMana / Mathf.Max(1, Unit.maxMana)) * 100f);

            RefreshStatus();

            Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(_root.panel, anchorWorld, cam);
            p.y = Mathf.Max(p.y, 205f);   // keep clear of the turn order and the banner
            DesiredX = p.x;   // left is set by SeparatePlates so neighbouring plates do not overlap
            _root.style.top = p.y;

            Vector2 f = RuntimePanelUtils.CameraTransformWorldToPanel(_ring.panel, feetWorld, cam);
            _ring.style.left = f.x;
            _ring.style.top = f.y;
        }

        private void RefreshStatus()
        {
            var effects = Unit.activeStatusEffects;
            string key = Unit.isDefending ? "D," : "";
            if (effects != null) foreach (var e in effects) key += (int)e.type + ":" + e.duration + ",";
            if (key == _lastStatusKey) return;
            _lastStatusKey = key;

            _statusRow.Clear();
            if (Unit.isDefending) _statusRow.Add(MakeTag("DEFENDING", new Color(0.66f, 0.76f, 0.93f), new Color(0.36f, 0.55f, 0.84f)));
            if (effects == null) return;
            foreach (var effect in effects) _statusRow.Add(MakeStatusChip(effect));
        }

        private static VisualElement MakeTag(string text, Color textColor, Color borderColor)
        {
            var tag = new Label(text) { pickingMode = PickingMode.Ignore };
            tag.AddToClassList("np-tag");
            tag.style.color = textColor;
            tag.style.borderTopColor = tag.style.borderBottomColor = tag.style.borderLeftColor = tag.style.borderRightColor = borderColor;
            return tag;
        }

        private static VisualElement MakeStatusChip(StatusEffect effect)
        {
            var chip = new VisualElement { pickingMode = PickingMode.Ignore };
            chip.AddToClassList("np-chip");

            Sprite custom = IconConfig != null ? IconConfig.GetIcon(effect.type) : null;
            if (custom != null)
            {
                chip.style.backgroundImage = new StyleBackground(custom);
            }
            else
            {
                chip.style.backgroundColor = StatusColor(effect.type);
                var letter = new Label(StatusLetter(effect.type)) { pickingMode = PickingMode.Ignore };
                letter.AddToClassList("np-chip-letter");
                chip.Add(letter);
            }

            var turns = new Label(effect.duration.ToString()) { pickingMode = PickingMode.Ignore };
            turns.AddToClassList("np-chip-turns");
            chip.Add(turns);
            return chip;
        }

        public void Destroy()
        {
            _root.RemoveFromHierarchy();
            _ring.RemoveFromHierarchy();
            Remove(this);
        }

        private static Color StatusColor(StatusEffectType type) => type switch
        {
            StatusEffectType.Burn => new Color(0.85f, 0.32f, 0.12f),
            StatusEffectType.Stun => new Color(0.42f, 0.46f, 0.88f),
            StatusEffectType.Weaken => new Color(0.55f, 0.18f, 0.72f),
            StatusEffectType.Fortify => new Color(0.15f, 0.58f, 0.70f),
            _ => new Color(0.5f, 0.5f, 0.5f),
        };

        private static string StatusLetter(StatusEffectType type) => type switch
        {
            StatusEffectType.Burn => "B",
            StatusEffectType.Stun => "S",
            StatusEffectType.Weaken => "W",
            StatusEffectType.Fortify => "F",
            _ => "?",
        };

        private static Color AffixColor(EnemyAffixType type) => type switch
        {
            EnemyAffixType.Armored => new Color(0.49f, 0.78f, 0.89f),
            EnemyAffixType.Berserker => new Color(1.00f, 0.40f, 0.36f),
            EnemyAffixType.Swift => new Color(0.66f, 1.00f, 0.47f),
            EnemyAffixType.Bulwark => new Color(0.75f, 0.63f, 0.38f),
            EnemyAffixType.Regenerating => new Color(0.27f, 0.87f, 0.53f),
            EnemyAffixType.Thorns => new Color(0.80f, 0.45f, 1.00f),
            EnemyAffixType.Vampiric => new Color(1.00f, 0.50f, 0.66f),
            EnemyAffixType.Enraged => new Color(1.00f, 0.60f, 0.20f),
            _ => new Color(1.00f, 0.87f, 0.27f),
        };
    }
}
