using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Battle HUD built with UI Toolkit. Layout lives in Resources/UI/GameUi.uxml, all styling in GameUi.uss
/// (dungeon theme variables at the top of that file). Reward/room cards are built by <see cref="RewardCardBuilder"/>.
/// </summary>
public class BattleUIManager : MonoBehaviour
{
    private BaseBattleManager _battleManager;

    [Header("UI Toolkit")]
    [SerializeField] private PanelSettings _panelSettings;
    [SerializeField] private VisualTreeAsset _gameUiAsset;

    private UIDocument _uiDocument;
    private VisualElement _root;
    private Label _messageLabel;
    private bool _loggedMissingUxml;

    // Bottom HUD
    private VisualElement _actionZone;
    private VisualElement _targetPanelZone;
    private Button _attackBtn;
    private Button _specialBtn;
    private Button _itemBtn;
    private Button _defendBtn;
    private Label _attackSub;
    private Label _specialSub;
    private Label _itemSub;
    private Label _commandTitle;
    private Label _commandRound;
    private Label _tipLabel;
    private int _turnsShown;
    private Button _claimBtn;
    private VisualElement _nameplateLayer;
    private Unit _selectedTarget;
    private bool _playerTurn;
    private bool _targetChosen;          // the player clicked a plate this turn
    private AttackAction _pendingAction; // a single-target skill waiting for the player to click an enemy
    private bool _buttonsWired;

    // Hero card (left)
    private VisualElement _statsPanelPortrait;
    private Label _statsPanelName;
    private ProgressBar _statsPanelHpBar;
    private ProgressBar _statsPanelMpBar;
    private VisualElement _statsPanelRows;
    private readonly Dictionary<string, Label> _statValueLabels = new Dictionary<string, Label>();
    private readonly Dictionary<string, VisualElement> _statRowElements = new Dictionary<string, VisualElement>();
    private readonly Dictionary<string, int> _lastStatValues = new Dictionary<string, int>();

    // Run HUD
    private Label _encounterLabel;
    private Label _goldLabel;
    private Label _roomBadge;
    private int _lastGold = -1;
    private VisualElement _runRewards;

    // Turn order bar
    private VisualElement _turnOrder;
    private readonly List<TurnChip> _turnChips = new List<TurnChip>();
    private const int MaxTurnChips = 10;

    private struct TurnChip
    {
        public Unit unit;
        public VisualElement chip;
        public VisualElement hpFill;
    }

    // Smooth bar animation
    private readonly Dictionary<ProgressBar, float> _barTargets = new Dictionary<ProgressBar, float>();
    private readonly List<ProgressBar> _barScratch = new List<ProgressBar>();
    private bool _barTickScheduled;

    // Tooltip
    private VisualElement _tooltip;
    private Label _tooltipTitle;
    private Label _tooltipBody;

    // Overlays
    private VisualElement _intermissionPanel;
    private Label _intermissionTitle;
    private Label _intermissionGold;
    private Label _intermissionInstruction;
    private VisualElement _upgradeChoices;
    private VisualElement _rerollContainer;
    private VisualElement _summaryPanel;
    private Label _summaryTitle;
    private Label _summaryStats;
    private VisualElement _summaryGrid;
    private VisualElement _summaryRelics;
    private Label _summaryRelicsTitle;

    // Battle log
    private VisualElement _battleLog;
    private Label _battleLogHeader;
    private ScrollView _battleLogScroll;
    private VisualElement _battleLogContent;
    private const int MaxLogEntries = 25;

    public enum LogEntryType { Normal, Crit, Heal, Death, System }

    // ─── SETUP ─────────────────────────────────────────────────────────────

    public void Initialize(BaseBattleManager battleManager)
    {
        _battleManager = battleManager;
        EnsureDocument();
        CacheUiReferences();
    }

    private bool EnsureUiReady()
    {
        if (_uiDocument == null) EnsureDocument();
        if (_root == null || _actionZone == null || _messageLabel == null) CacheUiReferences();
        return _root != null && _actionZone != null && _messageLabel != null;
    }

    private void EnsureDocument()
    {
        _uiDocument = GetComponent<UIDocument>();
        if (_uiDocument == null) _uiDocument = gameObject.AddComponent<UIDocument>();

        if (_uiDocument.panelSettings == null)
        {
            _uiDocument.panelSettings = _panelSettings != null
                ? _panelSettings
                : Resources.Load<PanelSettings>("UI/PanelSettings");

#if UNITY_EDITOR
            if (_uiDocument.panelSettings == null)
                _uiDocument.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI Toolkit/PanelSettings.asset");
#endif
            if (_uiDocument.panelSettings == null)
                _uiDocument.panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        }

        if (_uiDocument.visualTreeAsset == null)
        {
            _uiDocument.visualTreeAsset = _gameUiAsset != null
                ? _gameUiAsset
                : Resources.Load<VisualTreeAsset>("UI/GameUi");

            if (_uiDocument.visualTreeAsset == null && !_loggedMissingUxml)
            {
                _loggedMissingUxml = true;
                Debug.LogError("[BattleUIManager] Resources/UI/GameUi.uxml could not be loaded; the battle HUD will be empty.");
            }
        }
    }

    private void CacheUiReferences()
    {
        _root = _uiDocument != null ? _uiDocument.rootVisualElement : null;
        if (_root == null) return;

        _actionZone = _root.Q<VisualElement>("action-zone");
        _targetPanelZone = _root.Q<VisualElement>("target-panel-zone");
        _messageLabel = _root.Q<Label>("battle-message");

        // The UIDocument builds its tree a frame after creation; EnsureUiReady retries on the next call.
        if (_actionZone == null || _messageLabel == null) return;

        // Hero card
        _statsPanelPortrait = _root.Q<VisualElement>("stats-panel-portrait");
        _statsPanelName = _root.Q<Label>("stats-panel-name");
        _statsPanelHpBar = _root.Q<ProgressBar>("stats-panel-hp");
        _statsPanelMpBar = _root.Q<ProgressBar>("stats-panel-mp");
        _statsPanelRows = _root.Q<VisualElement>("stats-panel-rows");
        BuildStatRows();

        // Action buttons
        _attackBtn = _root.Q<Button>("attack-btn");
        _specialBtn = _root.Q<Button>("special-btn");
        _defendBtn = _root.Q<Button>("defend-btn");
        _itemBtn = _root.Q<Button>("item-btn");
        _attackSub = _root.Q<Label>("attack-btn-sub");
        _specialSub = _root.Q<Label>("special-btn-sub");
        _itemSub = _root.Q<Label>("item-btn-sub");
        _commandTitle = _root.Q<Label>("command-title");
        _commandRound = _root.Q<Label>("command-round");
        _tipLabel = _root.Q<Label>("tip-label");
        _claimBtn = _root.Q<Button>("claim-btn");
        _nameplateLayer = _root.Q<VisualElement>("nameplate-layer");
        Nameplates.SetLayer(_nameplateLayer, OnPlateClicked);

        // Tooltip
        _tooltip = _root.Q<VisualElement>("tooltip");
        _tooltipTitle = _root.Q<Label>("tooltip-title");
        _tooltipBody = _root.Q<Label>("tooltip-body");
        if (_tooltip != null) _tooltip.pickingMode = PickingMode.Ignore;

        if (!_buttonsWired)
        {
            if (_attackBtn != null) _attackBtn.clicked += HandleAttackClicked;
            if (_specialBtn != null) _specialBtn.clicked += HandleSpecialClicked;
            if (_defendBtn != null) _defendBtn.clicked += HandleDefendClicked;
            if (_itemBtn != null) _itemBtn.clicked += HandleItemClicked;

            RegisterTooltip(_attackBtn, DescribeAttackButton);
            RegisterTooltip(_specialBtn, DescribeSpecialButton);
            RegisterTooltip(_itemBtn, DescribeItemButton);
            RegisterTooltip(_defendBtn, () => ("Defend", "Take half damage until your next turn."));

            Button restartBtn = _root.Q<Button>("summary-restart");
            if (restartBtn != null) restartBtn.clicked += OnRestartClicked;

            _buttonsWired = true;
        }

        if (_targetPanelZone != null) _targetPanelZone.style.display = DisplayStyle.None;

        // Run HUD
        _encounterLabel = _root.Q<Label>("encounter-label");
        _goldLabel = _root.Q<Label>("gold-label");
        _roomBadge = _root.Q<Label>("room-badge");
        _runRewards = _root.Q<VisualElement>("run-rewards");
        _turnOrder = _root.Q<VisualElement>("turn-order");

        // Battle log
        _battleLog = _root.Q<VisualElement>("battle-log");
        _battleLogHeader = _root.Q<Label>("battle-log-header");
        _battleLogScroll = _root.Q<ScrollView>("battle-log-scroll");
        _battleLogContent = _root.Q<VisualElement>("battle-log-content");
        if (_battleLogHeader != null)
            _battleLogHeader.RegisterCallback<ClickEvent>(_ => ToggleBattleLog());

        // Overlays
        _rerollContainer = _root.Q<VisualElement>("reroll-container");
        _intermissionPanel = _root.Q<VisualElement>("intermission-panel");
        _intermissionTitle = _root.Q<Label>("intermission-title");
        _intermissionGold = _root.Q<Label>("intermission-gold");
        _intermissionInstruction = _root.Q<Label>("intermission-instruction");
        _upgradeChoices = _root.Q<VisualElement>("upgrade-choices");
        _summaryPanel = _root.Q<VisualElement>("run-summary-panel");
        _summaryTitle = _root.Q<Label>("summary-title");
        _summaryStats = _root.Q<Label>("summary-stats");
        _summaryGrid = _root.Q<VisualElement>("summary-grid");
        _summaryRelics = _root.Q<VisualElement>("summary-relics");
        _summaryRelicsTitle = _root.Q<Label>("summary-relics-title");
        HideOverlays();

        if (!_barTickScheduled)
        {
            _root.schedule.Execute(TickBars).Every(16);
            _barTickScheduled = true;
        }
    }

    // ─── PUBLIC HUD API ────────────────────────────────────────────────────

    public void SetupHealthBars(List<Unit> allUnits)
    {
        // World-space bars are FloatingHealthBar components on each unit; only the hero card lives here.
        Unit player = allUnits?.Find(u => u.isPlayer);
        if (player != null) UpdatePlayerCard(player);
    }

    public void UpdateHealth(Unit unit)
    {
        if (!EnsureUiReady() || unit == null) return;
        RefreshTurnChip(unit);
        if (unit.isPlayer) UpdatePlayerCard(unit);
    }

    public void UpdateStats(Unit unit)
    {
        // Stat display is handled by the hero card and the run-rewards strip.
    }

    public void ShowRestartMenu() => ShowRunSummary(false, 1, 0);

    public void UpdateRunHud(int current, int gold)
    {
        if (!EnsureUiReady()) return;

        if (_encounterLabel != null) _encounterLabel.text = $"Encounter {current}";

        if (_roomBadge != null)
        {
            _roomBadge.RemoveFromClassList("room-badge--boss");
            _roomBadge.RemoveFromClassList("room-badge--mini");
            if (current > 0 && current % 10 == 0)
            {
                _roomBadge.text = "BOSS";
                _roomBadge.AddToClassList("room-badge--boss");
            }
            else if (current > 0 && current % 5 == 0)
            {
                _roomBadge.text = "MINI-BOSS";
                _roomBadge.AddToClassList("room-badge--mini");
            }
        }

        if (_goldLabel != null)
        {
            _goldLabel.text = $"{gold} Gold";
            if (_lastGold >= 0 && gold != _lastGold) Pulse(_goldLabel, "gold-label--pulse");
        }
        _lastGold = gold;

        RefreshRunRewards();
    }

    // ─── HERO CARD ─────────────────────────────────────────────────────────

    private static readonly (string key, string label, string iconClass)[] StatRowDefs =
    {
        ("atk", "ATK", "icon-sword"),
        ("def", "DEF", "icon-shield"),
        ("spd", "SPD", "icon-energy"),
        ("lck", "LCK", "icon-clover"),
    };

    private void BuildStatRows()
    {
        if (_statsPanelRows == null || _statValueLabels.Count > 0) return;

        foreach (var def in StatRowDefs)
        {
            var row = new VisualElement();
            row.AddToClassList("stats-panel-row");

            var icon = new VisualElement();
            icon.AddToClassList("stats-panel-row-icon");
            icon.AddToClassList(def.iconClass);
            row.Add(icon);

            var label = new Label(def.label);
            label.AddToClassList("stats-panel-row-label");
            row.Add(label);

            var value = new Label("-");
            value.AddToClassList("stats-panel-row-value");
            row.Add(value);

            _statsPanelRows.Add(row);
            _statValueLabels[def.key] = value;
            _statRowElements[def.key] = row;
        }
    }

    private void UpdatePlayerCard(Unit u)
    {
        if (u == null) return;

        if (_statsPanelName != null) _statsPanelName.text = u.unitName;
        if (_statsPanelPortrait != null && u.portrait != null)
            _statsPanelPortrait.style.backgroundImage = new StyleBackground(u.portrait);

        SetProgressBar(_statsPanelHpBar, u.currentHealth, u.maxHealth);
        if (_statsPanelMpBar != null)
        {
            _statsPanelMpBar.style.display = u.maxMana > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (u.maxMana > 0) SetProgressBar(_statsPanelMpBar, u.currentMana, u.maxMana);
        }

        SetStat("atk", u.attack);
        SetStat("def", u.defense);
        SetStat("spd", u.speed);
        SetStat("lck", u.luck);
    }

    private void SetStat(string key, int value)
    {
        if (!_statValueLabels.TryGetValue(key, out Label label)) return;
        label.text = value.ToString();

        if (_lastStatValues.TryGetValue(key, out int previous) && previous != value)
        {
            VisualElement row = _statRowElements[key];
            string cls = value > previous ? "stats-panel-row--up" : "stats-panel-row--down";
            row.AddToClassList(cls);
            row.schedule.Execute(() => row.RemoveFromClassList(cls)).StartingIn(700);
        }
        _lastStatValues[key] = value;
    }

    // ─── SMOOTH PROGRESS BARS ──────────────────────────────────────────────

    private void SetProgressBar(ProgressBar bar, float current, float max)
    {
        if (bar == null) return;

        float safeMax = Mathf.Max(1f, max);
        float clamped = Mathf.Clamp(current, 0f, safeMax);
        float target = clamped / safeMax * 100f;

        bar.lowValue = 0f;
        bar.highValue = 100f;
        bar.title = $"{Mathf.RoundToInt(clamped)}/{Mathf.RoundToInt(safeMax)}";

        if (_barTargets.ContainsKey(bar))
        {
            _barTargets[bar] = target;   // TickBars eases towards it
        }
        else
        {
            _barTargets[bar] = target;
            bar.value = target;
            ApplyFillVisibility(bar);
        }
    }

    private void TickBars()
    {
        if (_barTargets.Count == 0) return;
        _barScratch.Clear();
        _barScratch.AddRange(_barTargets.Keys);

        foreach (ProgressBar bar in _barScratch)
        {
            float target = _barTargets[bar];
            float diff = target - bar.value;
            if (Mathf.Abs(diff) < 0.05f) continue;

            bar.value = Mathf.Abs(diff) < 0.4f ? target : Mathf.Lerp(bar.value, target, 0.16f);
            ApplyFillVisibility(bar);
        }
    }

    private static void ApplyFillVisibility(ProgressBar bar)
    {
        VisualElement fill = bar.Q(className: "unity-progress-bar__progress");
        if (fill != null) fill.style.opacity = bar.value > 0.05f ? 1f : 0f;
    }

    // ─── TURN ORDER ────────────────────────────────────────────────────────

    /// <summary>Rebuilds the turn bar: the active unit, the rest of this round, then a divider and next round.</summary>
    public void UpdateTurnOrder(Unit active, IReadOnlyList<Unit> remaining, IReadOnlyList<Unit> nextRound)
    {
        if (!EnsureUiReady() || _turnOrder == null) return;

        _turnOrder.Clear();
        _turnChips.Clear();
        if (_turnOrder.parent != null) _turnOrder.parent.style.display = DisplayStyle.Flex;

        if (active != null) AddTurnChip(active, true);
        if (remaining != null)
            foreach (Unit u in remaining)
                if (u != null && !u.isDead && _turnChips.Count < MaxTurnChips) AddTurnChip(u, false);

        if (nextRound != null && _turnChips.Count < MaxTurnChips)
        {
            var divider = new VisualElement();
            divider.AddToClassList("turn-divider");
            _turnOrder.Add(divider);
            foreach (Unit u in nextRound)
                if (u != null && !u.isDead && _turnChips.Count < MaxTurnChips) AddTurnChip(u, false);
        }
    }

    public void ClearTurnOrder()
    {
        _turnOrder?.Clear();
        _turnChips.Clear();
        if (_turnOrder?.parent != null) _turnOrder.parent.style.display = DisplayStyle.None;   // hide the empty panel
    }

    private void AddTurnChip(Unit u, bool active)
    {
        var chip = new VisualElement();
        chip.AddToClassList("turn-chip");
        chip.AddToClassList(u.isPlayer ? "turn-chip--player" : "turn-chip--enemy");

        if (u.portrait != null)
        {
            chip.style.backgroundImage = new StyleBackground(u.portrait);
        }
        else
        {
            string initial = string.IsNullOrEmpty(u.unitName) ? "?" : u.unitName.Substring(0, 1).ToUpperInvariant();
            var letter = new Label(initial);
            letter.AddToClassList("turn-chip-letter");
            chip.Add(letter);
        }

        var hpBar = new VisualElement();
        hpBar.AddToClassList("turn-chip-hp");
        var hpFill = new VisualElement();
        hpFill.AddToClassList("turn-chip-hp-fill");
        hpFill.style.width = Length.Percent(HealthPercent(u));
        hpBar.Add(hpFill);
        chip.Add(hpBar);

        RegisterTooltip(chip, () => u == null ? ("", "") :
            (u.unitName, $"HP {u.currentHealth}/{u.maxHealth}   SPD {u.speed}"));

        _turnOrder.Add(chip);
        _turnChips.Add(new TurnChip { unit = u, chip = chip, hpFill = hpFill });

        if (active)
            chip.schedule.Execute(() => chip.AddToClassList("turn-chip--active")).StartingIn(30);
    }

    private void RefreshTurnChip(Unit u)
    {
        foreach (TurnChip tc in _turnChips)
        {
            if (tc.unit != u) continue;
            tc.hpFill.style.width = Length.Percent(HealthPercent(u));
            if (u.isDead) tc.chip.AddToClassList("turn-chip--dead");
        }
    }

    private static float HealthPercent(Unit u) =>
        Mathf.Clamp01(u.currentHealth / Mathf.Max(1f, u.maxHealth)) * 100f;

    // ─── TOOLTIPS ──────────────────────────────────────────────────────────

    private void RegisterTooltip(VisualElement target, Func<(string title, string body)> getter)
    {
        if (target == null) return;
        target.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(target, getter()));
        target.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
        target.RegisterCallback<DetachFromPanelEvent>(_ => HideTooltip());
    }

    private void ShowTooltip(VisualElement anchor, (string title, string body) content)
    {
        if (_tooltip == null || _root == null) return;
        if (string.IsNullOrEmpty(content.title) && string.IsNullOrEmpty(content.body)) return;

        _tooltipTitle.text = content.title;
        _tooltipTitle.style.display = string.IsNullOrEmpty(content.title) ? DisplayStyle.None : DisplayStyle.Flex;
        _tooltipBody.text = content.body;
        _tooltipBody.style.display = string.IsNullOrEmpty(content.body) ? DisplayStyle.None : DisplayStyle.Flex;

        bool fixedSpot = anchor.GetFirstAncestorOfType<VisualElement>() != null && IsInsideCommandPanel(anchor);
        _tooltip.EnableInClassList("tooltip--fixed", fixedSpot);
        if (fixedSpot)
        {
            _tooltip.style.left = StyleKeyword.Null;
            _tooltip.style.top = StyleKeyword.Null;
            _tooltip.style.bottom = StyleKeyword.Null;
            _tooltip.style.display = DisplayStyle.Flex;
            return;
        }

        Rect rootRect = _root.worldBound;
        Rect a = anchor.worldBound;
        float left = Mathf.Clamp(a.xMin - rootRect.xMin, 8f, Mathf.Max(8f, rootRect.width - 280f));
        _tooltip.style.left = left;

        bool below = a.center.y < rootRect.center.y;   // top-of-screen anchors get the tooltip underneath
        if (below)
        {
            _tooltip.style.top = a.yMax - rootRect.yMin + 8f;
            _tooltip.style.bottom = StyleKeyword.Auto;
        }
        else
        {
            _tooltip.style.bottom = rootRect.yMax - a.yMin + 8f;
            _tooltip.style.top = StyleKeyword.Auto;
        }
        _tooltip.style.display = DisplayStyle.Flex;
    }

    private static bool IsInsideCommandPanel(VisualElement e)
    {
        for (VisualElement p = e; p != null; p = p.parent)
            if (p.ClassListContains("bottom-hud")) return true;
        return false;
    }

    private void HideTooltip()
    {
        if (_tooltip != null) _tooltip.style.display = DisplayStyle.None;
    }

    private (string, string) DescribeAttackButton()
    {
        AttackAction atk = _battleManager?.GetActiveUnit()?.basicAttack;
        return atk == null ? ("Attack", "Strike an enemy.") : (atk.actionName, DescribeAttack(atk));
    }

    private (string, string) DescribeSpecialButton()
    {
        Unit u = _battleManager?.GetActiveUnit();
        int count = u?.specialAttacks?.Count ?? 0;
        string mana = u != null && u.maxMana > 0 ? $"\nMana: {u.currentMana}/{u.maxMana}" : "";
        return ("Special", $"Spend mana on a powerful skill. {count} available.{mana}");
    }

    private (string, string) DescribeItemButton()
    {
        int count = _battleManager?.GetActiveUnit()?.inventory?.Count ?? 0;
        return ("Items", count > 0 ? $"Use a consumable. {count} in your bag." : "Your bag is empty.");
    }

    public static string DescribeAttack(AttackAction a)
    {
        var sb = new StringBuilder();
        if (a.isSummon)
        {
            sb.Append($"Summons {a.summonCount} minion{(a.summonCount == 1 ? "" : "s")}.");
        }
        else
        {
            sb.Append($"{a.minDamage}-{a.maxDamage} base damage");
            if (a.hitCount > 1) sb.Append($" x{a.hitCount} hits");
            if (a.projectileCount > 1) sb.Append($" x{a.projectileCount} projectiles");
            sb.Append(a.isAoE ? "\nHits all enemies." : "\nSingle target.");
        }
        if (a.manaCost > 0) sb.Append($"\nCosts {a.manaCost} MP.");
        if (a.appliesStatus != StatusEffectType.None)
            sb.Append($"\n{Mathf.RoundToInt(a.statusChance * 100f)}% to inflict {a.appliesStatus} for {a.statusDuration} turns.");
        if (a.lifeStealPercent > 0f) sb.Append("\nDrains life from the target.");
        return sb.ToString();
    }

    // ─── FIRST-TIME HINTS ──────────────────────────────────────────────────

    /// <summary>One-line hints that appear once per install (flags in PlayerPrefs), at the moment they matter.</summary>
    private void MaybeShowTips(Unit hero)
    {
        if (_turnsShown == 1)
            ShowTip("target", "Tip: click an enemy to choose your target, then press Attack.");
        else if (_turnsShown == 2)
            ShowTip("camera", "Tip: press C to change the camera angle.");
        else if (hero.specialAttacks != null && hero.specialAttacks.Count > 0 && hero.maxMana > 0)
            ShowTip("mana", "Tip: Specials cost mana. You regain 10% of your max mana every turn.");
        else if (hero.inventory != null && hero.inventory.Count > 0 && _turnsShown >= 3)
            ShowTip("items", "Tip: potions are kept for the whole run. Use them from Item.");
    }

    private void ShowTip(string key, string text)
    {
        if (_tipLabel == null) return;
        string flag = "tip_" + key;
        try { if (PlayerPrefs.GetInt(flag, 0) == 1) return; PlayerPrefs.SetInt(flag, 1); } catch { }

        _tipLabel.text = text;
        _tipLabel.AddToClassList("tip-label--shown");
        _tipLabel.schedule.Execute(() => _tipLabel.RemoveFromClassList("tip-label--shown")).StartingIn(7000);
    }

    // ─── COMMAND MENU ──────────────────────────────────────────────────────

    public void ShowCommandMenu()
    {
        if (!EnsureUiReady() || _battleManager == null) return;

        _targetPanelZone?.Clear();
        if (_targetPanelZone != null) _targetPanelZone.style.display = DisplayStyle.None;
        if (_actionZone != null) _actionZone.style.display = DisplayStyle.Flex;

        Unit activeUnit = _battleManager.GetActiveUnit();
        if (activeUnit == null) return;

        UpdatePlayerCard(activeUnit);

        bool hasSpecials = activeUnit.specialAttacks != null && activeUnit.specialAttacks.Count > 0;
        bool hasItems = activeUnit.inventory != null && activeUnit.inventory.Count > 0;

        _playerTurn = true;
        _turnsShown++;
        MaybeShowTips(activeUnit);
        _pendingAction = null;
        Nameplates.SetPicking(false);
        if (_commandRound != null && _battleManager != null) _commandRound.text = $"Round {_battleManager.BattleRounds}";

        // Default target: keep the previous one while it is alive, otherwise the first living enemy.
        var living = _battleManager.GetAliveEnemies();
        if (_selectedTarget == null || _selectedTarget.isDead || !living.Contains(_selectedTarget))
            _selectedTarget = living.Count > 0 ? living[0] : null;
        Nameplates.SetTarget(_selectedTarget);

        AttackAction basic = activeUnit.basicAttack;
        if (_attackSub != null)
            _attackSub.text = basic == null ? "" : basic.isSummon ? "Summon" : $"{basic.minDamage}\u2013{basic.maxDamage} dmg";
        if (_specialSub != null)
        {
            int n = activeUnit.specialAttacks?.Count ?? 0;
            AttackAction first = n > 0 ? activeUnit.specialAttacks[0] : null;
            _specialSub.text = n == 0 ? "None learned"
                : n == 1 && first != null ? (first.manaCost > 0 ? $"{first.actionName} \u00b7 {first.manaCost} MP" : first.actionName)
                : $"{n} known";
        }
        if (_itemSub != null)
            _itemSub.text = hasItems ? $"{activeUnit.inventory.Count} in pack" : "Empty";

        _attackBtn?.SetEnabled(true);
        _specialBtn?.SetEnabled(hasSpecials);
        _itemBtn?.SetEnabled(hasItems);
        _defendBtn?.SetEnabled(true);
    }

    public void HideCommandMenu()
    {
        if (!EnsureUiReady()) return;
        HideTooltip();
        _playerTurn = false;
        _targetChosen = false;
        _pendingAction = null;
        Nameplates.SetPicking(false);
        Nameplates.SetTarget(null);
        _targetPanelZone?.Clear();
        if (_targetPanelZone != null) _targetPanelZone.style.display = DisplayStyle.None;
        if (_actionZone != null) _actionZone.style.display = DisplayStyle.None;
    }

    public void EnableAttackButton(bool enable)
    {
        if (!EnsureUiReady()) return;

        if (enable)
        {
            ShowCommandMenu();
            return;
        }

        _playerTurn = false;
        _targetChosen = false;
        _pendingAction = null;
        Nameplates.SetPicking(false);
        Nameplates.SetTarget(null);
        _targetPanelZone?.Clear();
        if (_targetPanelZone != null) _targetPanelZone.style.display = DisplayStyle.None;
        if (_actionZone != null) _actionZone.style.display = DisplayStyle.Flex;
        _attackBtn?.SetEnabled(false);
        _specialBtn?.SetEnabled(false);
        _itemBtn?.SetEnabled(false);
        _defendBtn?.SetEnabled(false);
    }

    private void HandleAttackClicked()
    {
        if (_battleManager == null || !_playerTurn) return;
        Unit activeUnit = _battleManager.GetActiveUnit();
        if (activeUnit == null) return;

        if (activeUnit.basicAttack == null)
        {
            SetMessage("No basic attack assigned!");
            return;
        }
        SelectTargetFor(activeUnit.basicAttack);
    }

    private void HandleSpecialClicked()
    {
        if (!_playerTurn) return;
        Unit activeUnit = _battleManager?.GetActiveUnit();
        if (activeUnit != null) ShowSpecialSelection(activeUnit);
    }

    private void HandleItemClicked()
    {
        if (!_playerTurn) return;
        Unit activeUnit = _battleManager?.GetActiveUnit();
        if (activeUnit != null) ShowItemSelection(activeUnit);
    }

    private void HandleDefendClicked()
    {
        if (_playerTurn) _battleManager?.PlayerDefend();
    }

    /// <summary>
    /// AoE attacks fire at once. Single-target skills use the enemy whose plate was clicked this turn; if there are
    /// several enemies and none was clicked yet, the skill waits and the next enemy plate click fires it.
    /// </summary>
    private void SelectTargetFor(AttackAction action)
    {
        var enemies = _battleManager.GetAliveEnemies();
        if (enemies.Count == 0) return;

        if (action.isAoE || enemies.Count == 1 || (_targetChosen && _selectedTarget != null && !_selectedTarget.isDead))
        {
            Unit target = action.isAoE || _selectedTarget == null || _selectedTarget.isDead ? enemies[0] : _selectedTarget;
            _battleManager.PlayerSelectTarget(target, action);
            return;
        }

        ShowTargetPrompt(action, enemies);
    }

    /// <summary>
    /// Lists the living enemies under Commands (name + HP). Clicking a row, or an enemy's nameplate, fires the
    /// pending skill. Hovering a row highlights that enemy in the scene.
    /// </summary>
    private void ShowTargetPrompt(AttackAction action, List<Unit> enemies)
    {
        OpenSubMenu(() =>
        {
            _pendingAction = action;
            Nameplates.SetPicking(true);
            SetMessage($"Choose a target for {action.actionName}.");

            foreach (Unit enemy in enemies)
            {
                Unit captured = enemy;
                var row = new Button(() => ChooseTarget(captured));
                row.AddToClassList("menu-button");
                row.AddToClassList("target-row");

                var nameLabel = new Label(PrettyName(enemy.unitName)) { pickingMode = PickingMode.Ignore };
                nameLabel.AddToClassList("target-row-name");
                row.Add(nameLabel);
                var hpLabel = new Label($"{enemy.currentHealth}/{enemy.maxHealth}") { pickingMode = PickingMode.Ignore };
                hpLabel.AddToClassList("target-row-hp");
                row.Add(hpLabel);

                row.RegisterCallback<PointerEnterEvent>(_ => Nameplates.SetTarget(captured));   // highlight it in the scene
                _targetPanelZone.Add(row);
            }
            SpawnMenuButton("Back", true, ShowCommandMenu);
        });
    }

    private void OnPlateClicked(Unit unit)
    {
        if (!_playerTurn || unit == null || unit.isPlayer || unit.isDead) return;
        ChooseTarget(unit);
    }

    private void ChooseTarget(Unit unit)
    {
        _selectedTarget = unit;
        _targetChosen = true;
        Nameplates.SetTarget(unit);

        if (_pendingAction != null)
        {
            AttackAction action = _pendingAction;
            _pendingAction = null;
            Nameplates.SetPicking(false);
            _targetPanelZone?.Clear();
            _battleManager.PlayerSelectTarget(unit, action);
        }
    }

    // ─── SUB-MENUS ─────────────────────────────────────────────────────────

    private void OpenSubMenu(Action populate)
    {
        if (_actionZone != null) _actionZone.style.display = DisplayStyle.None;
        if (_targetPanelZone != null) _targetPanelZone.style.display = DisplayStyle.Flex;
        HideTooltip();
        _targetPanelZone?.Clear();
        populate();
    }

    private void SpawnMenuButton(string label, bool interactable, Action onClick,
                                 string tooltipTitle = null, string tooltipBody = null, Action onHover = null)
    {
        if (!EnsureUiReady() || _targetPanelZone == null) return;

        var btn = new Button(() => onClick?.Invoke()) { text = label };
        btn.SetEnabled(interactable);
        btn.AddToClassList("menu-button");
        if (onHover != null) btn.RegisterCallback<PointerEnterEvent>(_ => onHover());
        if (tooltipTitle != null || tooltipBody != null)
            RegisterTooltip(btn, () => (tooltipTitle ?? "", tooltipBody ?? ""));
        _targetPanelZone.Add(btn);
    }

    private void ShowSpecialSelection(Unit activeUnit)
    {
        OpenSubMenu(() =>
        {
            foreach (AttackAction attack in activeUnit.specialAttacks)
            {
                if (attack == null) continue;
                bool canAfford = activeUnit.currentMana >= attack.manaCost;
                string costStr = attack.manaCost > 0 ? $" ({attack.manaCost} MP)" : "";
                AttackAction captured = attack;

                SpawnMenuButton(attack.actionName + costStr, canAfford, () =>
                {
                    _targetPanelZone?.Clear();
                    SelectTargetFor(captured);
                }, attack.actionName, DescribeAttack(attack));
            }
            SpawnMenuButton("Back", true, ShowCommandMenu);
        });
    }

    private void ShowItemSelection(Unit activeUnit)
    {
        OpenSubMenu(() =>
        {
            var grouped = new Dictionary<BattleItem, int>();
            foreach (BattleItem item in activeUnit.inventory)
            {
                if (item == null) continue;
                grouped[item] = grouped.TryGetValue(item, out int n) ? n + 1 : 1;
            }

            foreach (var kvp in grouped)
            {
                BattleItem captured = kvp.Key;
                SpawnMenuButton($"{captured.itemName} x{kvp.Value}", true, () =>
                {
                    _targetPanelZone?.Clear();
                    _battleManager.PlayerUseItem(captured);
                });
            }

            if (grouped.Count == 0) SpawnMenuButton("No items!", false, null);
            SpawnMenuButton("Back", true, ShowCommandMenu);
        });
    }

    public void ClearTargetButtons()
    {
        if (!EnsureUiReady()) return;
        HideTooltip();
        _targetPanelZone?.Clear();
        if (_targetPanelZone != null) _targetPanelZone.style.display = DisplayStyle.None;
    }

    // ─── MESSAGE + LOG ─────────────────────────────────────────────────────

    public void SetMessage(string msg)
    {
        if (!EnsureUiReady()) return;

        if (_messageLabel != null)
        {
            _messageLabel.text = msg;
            _messageLabel.style.display = string.IsNullOrEmpty(msg) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        if (string.IsNullOrEmpty(msg)) return;

        LogEntryType logType = LogEntryType.Normal;
        string lower = msg.ToLowerInvariant();
        if (lower.Contains("critical")) logType = LogEntryType.Crit;
        else if (lower.Contains("dies") || lower.Contains("defeated") || lower.Contains("dead")) logType = LogEntryType.Death;
        else if (lower.Contains("regenerates") || lower.Contains("heals") || lower.Contains("restored") || lower.Contains("burns for")) logType = LogEntryType.Heal;
        else if (lower.Contains("encounter") || lower.Contains("fight!") || lower.Contains("battle")) logType = LogEntryType.System;
        if (lower.Contains("burns for")) logType = LogEntryType.Death;
        AddLogEntry(msg, logType);
    }

    public void ClearBattleLog()
    {
        _battleLogContent?.Clear();
        HideClaimButton();
    }

    /// <summary>Shows the gold button in the combat-log header (e.g. "CLAIM SPOILS") until it is clicked.</summary>
    public void ShowClaimButton(string text, Action onClick)
    {
        if (!EnsureUiReady() || _claimBtn == null) { onClick?.Invoke(); return; }
        _claimBtn.text = text;
        _claimBtn.clickable = new Clickable(() =>
        {
            HideClaimButton();
            onClick?.Invoke();
        });
        _claimBtn.AddToClassList("claim-btn--shown");
    }

    public void HideClaimButton() => _claimBtn?.RemoveFromClassList("claim-btn--shown");

    /// <summary>Adds an entry to the battle log (newest at top, older entries fade).</summary>
    public void AddLogEntry(string msg, LogEntryType type = LogEntryType.Normal)
    {
        if (_battleLogContent == null) return;

        var entry = new Label(msg);
        entry.AddToClassList("battle-log-entry");
        switch (type)
        {
            case LogEntryType.Crit: entry.AddToClassList("log-crit"); break;
            case LogEntryType.Heal: entry.AddToClassList("log-heal"); break;
            case LogEntryType.Death: entry.AddToClassList("log-death"); break;
            case LogEntryType.System: entry.AddToClassList("log-system"); break;
        }

        _battleLogContent.Insert(0, entry);
        while (_battleLogContent.childCount > MaxLogEntries)
            _battleLogContent.RemoveAt(_battleLogContent.childCount - 1);

        int count = _battleLogContent.childCount;
        for (int i = 0; i < count; i++)
            _battleLogContent[i].style.opacity = Mathf.Lerp(1f, 0.45f, count <= 1 ? 0f : (float)i / (count - 1));
    }

    private void ToggleBattleLog()
    {
        if (_battleLogScroll == null) return;
        bool show = _battleLogScroll.resolvedStyle.display == DisplayStyle.None;
        _battleLogScroll.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        if (_battleLogHeader != null) _battleLogHeader.text = show ? "BATTLE LOG  -" : "BATTLE LOG  +";
    }

    // ─── RUN REWARDS STRIP ─────────────────────────────────────────────────

    private static bool IsTrait(UpgradeType t) =>
        t == UpgradeType.Vampiric || t == UpgradeType.Thorns ||
        t == UpgradeType.Regenerating || t == UpgradeType.Enraged;

    public void RefreshRunRewards()
    {
        if (_runRewards == null) return;
        RunManager rm = RunManager.Instance;
        if (rm == null) { _runRewards.Clear(); return; }

        // Tally chosen upgrades by display name, remembering the category for colouring.
        var entries = new Dictionary<string, (int count, string css)>();
        void Tally(string name, string css)
        {
            entries[name] = entries.TryGetValue(name, out var e) ? (e.count + 1, css) : (1, css);
        }

        foreach (var u in rm.AppliedUpgrades)
            Tally(u.name, IsTrait(u.upgradeType) ? "reward-pill--trait" : "reward-pill--stat");
        foreach (var u in rm.AppliedAttackUpgrades)
            Tally(u.name, "reward-pill--attack");

        _runRewards.Clear();

        // Relics first (gold), then the stat/trait boosts each relic grants.
        foreach (var kvp in rm.EquippedRelics)
        {
            var pill = new Label(kvp.Value.relicName);
            pill.AddToClassList("reward-pill");
            pill.AddToClassList("reward-pill--relic");
            _runRewards.Add(pill);

            foreach (var boost in kvp.Value.statBoosts)
                if (!entries.ContainsKey(boost.name))
                    entries[boost.name] = (1, IsTrait(boost.upgradeType) ? "reward-pill--trait" : "reward-pill--stat");
        }

        foreach (var kvp in entries)
        {
            string text = kvp.Value.count > 1 ? $"{kvp.Key} x{kvp.Value.count}" : kvp.Key;
            var pill = new Label(text);
            pill.AddToClassList("reward-pill");
            pill.AddToClassList(kvp.Value.css);
            _runRewards.Add(pill);
        }
    }

    // ─── OVERLAYS ──────────────────────────────────────────────────────────

    public void HideOverlays()
    {
        HidePanel(_intermissionPanel);
        HidePanel(_summaryPanel);
        SetOverlayOpen(false);
    }

    /// <summary>While a full-screen overlay is open, hide the HUD panels and world-space nameplates behind it.</summary>
    private void SetOverlayOpen(bool open)
    {
        _root?.EnableInClassList("overlay-open", open);
        FloatingHealthBar.SetCanvasVisible(!open);
    }

    private static void HidePanel(VisualElement panel)
    {
        if (panel == null) return;
        panel.RemoveFromClassList("overlay-panel--shown");
        panel.style.display = DisplayStyle.None;
    }

    private static void ShowPanel(VisualElement panel)
    {
        if (panel == null) return;
        panel.style.display = DisplayStyle.Flex;
        panel.schedule.Execute(() => panel.AddToClassList("overlay-panel--shown")).StartingIn(10);
    }

    /// <summary>Slides a container's children in one after another.</summary>
    private static void AnimateIn(VisualElement container)
    {
        if (container == null) return;
        for (int i = 0; i < container.childCount; i++)
        {
            VisualElement child = container[i];
            child.AddToClassList("anim-hidden");
            child.schedule.Execute(() => child.RemoveFromClassList("anim-hidden")).StartingIn(80 + i * 90);
        }
    }

    private static void Pulse(VisualElement element, string cssClass)
    {
        element.AddToClassList(cssClass);
        element.schedule.Execute(() => element.RemoveFromClassList(cssClass)).StartingIn(220);
    }

    /// <summary>Resets the shared choice panel for a new screen.</summary>
    private void BeginChoiceScreen(string title, string subtitle, string instruction)
    {
        HideOverlays();
        ClearTargetButtons();
        ClearTurnOrder();

        if (_intermissionTitle != null) _intermissionTitle.text = title;
        if (_intermissionGold != null) _intermissionGold.text = subtitle;
        if (_intermissionInstruction != null)
        {
            _intermissionInstruction.text = instruction ?? "";
            _intermissionInstruction.style.display = string.IsNullOrEmpty(instruction) ? DisplayStyle.None : DisplayStyle.Flex;
        }
        _upgradeChoices?.Clear();
        _rerollContainer?.Clear();
        _root?.Q<VisualElement>("path-track")?.RemoveFromHierarchy();
    }

    private void EndChoiceScreen()
    {
        AnimateIn(_upgradeChoices);
        SetOverlayOpen(true);
        ShowPanel(_intermissionPanel);
    }

    /// <summary>Inserts spaces into CamelCase unit names ("SkeletonKnight 1" -> "Skeleton Knight 1").</summary>
    public static string PrettyName(string name) =>
        string.IsNullOrEmpty(name) ? "" : System.Text.RegularExpressions.Regex.Replace(name, "(?<=[a-z])(?=[A-Z])", " ");

    private static void AddPills(VisualElement parent, IEnumerable<string> names, string extraClass)
    {
        if (parent == null || names == null) return;
        var seen = new Dictionary<string, int>();
        var order = new List<string>();
        foreach (string raw in names)
        {
            // "Skeleton Knight 1" and "Skeleton Knight 2" collapse into "Skeleton Knight x2".
            string n = System.Text.RegularExpressions.Regex.Replace(PrettyName(raw), @"\s*\d+$", "");
            if (!seen.ContainsKey(n)) { seen[n] = 0; order.Add(n); }
            seen[n]++;
        }
        foreach (string n in order)
        {
            var pill = new Label(seen[n] > 1 ? $"{n} x{seen[n]}" : n);
            pill.AddToClassList("loot-pill");
            pill.AddToClassList(extraClass);
            parent.Add(pill);
        }
    }

    /// <summary>
    /// Loot screen: click a card to select it, then confirm with the Take button.
    /// <paramref name="rounds"/> and <paramref name="foes"/> are optional run-through details.
    /// </summary>
    public void ShowIntermission(int clearedEncounter, int goldEarned, List<RewardChoice> choices,
                                 Action<RewardChoice> onChoose, Action onReroll = null,
                                 int rounds = 0, IEnumerable<string> foes = null)
    {
        if (!EnsureUiReady()) return;
        string sub = rounds > 0 ? $"Cleared in {rounds} round{(rounds == 1 ? "" : "s")}" : "";
        BeginChoiceScreen($"Spoils of Encounter {clearedEncounter}", sub, "CHOOSE ONE REWARD");

        if (_upgradeChoices == null) { EndChoiceScreen(); return; }

        RewardChoice selected = null;
        VisualElement selectedCard = null;
        Button takeBtn = null;
        Label hint = null;

        foreach (RewardChoice choice in choices)
        {
            _upgradeChoices.Add(RewardCardBuilder.CreateRewardCard(choice, null, 0, -1, (card, picked) =>
            {
                selectedCard?.RemoveFromClassList("reward-card--selected");
                selected = picked;
                selectedCard = card;
                card.AddToClassList("reward-card--selected");
                if (takeBtn != null)
                {
                    takeBtn.SetEnabled(true);
                    takeBtn.text = $"TAKE {picked.name.ToUpperInvariant()}";
                }
                if (hint != null) hint.text = "Confirm your choice, or pick a different card.";
            }));
        }

        if (_rerollContainer != null)
        {
            // Row: also collected + foes defeated
            var collected = new VisualElement();
            collected.AddToClassList("loot-row");
            var lbl = new Label("ALSO COLLECTED");
            lbl.AddToClassList("loot-row-label");
            collected.Add(lbl);
            var gold = new Label($"+{goldEarned} Gold");
            gold.AddToClassList("loot-pill");
            gold.AddToClassList("loot-pill--gold");
            collected.Add(gold);
            if (foes != null)
            {
                var foesLbl = new Label("FOES DEFEATED");
                foesLbl.AddToClassList("loot-row-label");
                foesLbl.AddToClassList("loot-row-label--gap");
                collected.Add(foesLbl);
                AddPills(collected, foes, "loot-pill--foe");
            }
            _rerollContainer.Add(collected);

            // Footer: reroll | hint | take (becomes "claimed" + continue after taking)
            var footer = new VisualElement();
            footer.AddToClassList("loot-footer");

            var rerollBtn = new Button { text = onReroll != null ? "REROLL \u00b7 1 LEFT" : "REROLL \u00b7 0 LEFT" };
            rerollBtn.AddToClassList("menu-button");
            rerollBtn.AddToClassList("reroll-button");
            rerollBtn.SetEnabled(onReroll != null);
            rerollBtn.clicked += () =>
            {
                rerollBtn.SetEnabled(false);
                onReroll?.Invoke();
            };
            footer.Add(rerollBtn);

            hint = new Label("Select a reward.");
            hint.AddToClassList("loot-hint");
            footer.Add(hint);

            takeBtn = new Button(() =>
            {
                if (selected == null) return;
                RewardChoice taken = selected;

                foreach (VisualElement card in _upgradeChoices.Children()) card.pickingMode = PickingMode.Ignore;
                footer.Clear();

                var done = new Label($"\u2713  {taken.name} claimed.");
                done.AddToClassList("loot-claimed");
                footer.Add(done);

                var cont = new Button(() => onChoose?.Invoke(taken)) { text = "CONTINUE  >" };
                cont.AddToClassList("reward-button");
                cont.AddToClassList("loot-take");
                footer.Add(cont);
            }) { text = "TAKE REWARD" };
            takeBtn.AddToClassList("reward-button");
            takeBtn.AddToClassList("loot-take");
            takeBtn.SetEnabled(false);
            footer.Add(takeBtn);

            _rerollContainer.Add(footer);
        }
        EndChoiceScreen();
    }

    /// <summary>Shows the choice panel for a non-combat event or merchant.</summary>
    public void ShowEventOverlay(string title, string subtitle, List<RewardChoice> choices,
                                 Action<RewardChoice> onChoose, int currentGold = -1, Action onSkip = null)
    {
        if (!EnsureUiReady()) return;
        BeginChoiceScreen(title, subtitle, null);

        bool isMerchant = choices != null && choices.Count > 0 && choices[0].goldCost > 0;

        if (_upgradeChoices != null)
        {
            if (choices != null)
            {
                foreach (RewardChoice choice in choices)
                {
                    _upgradeChoices.Add(RewardCardBuilder.CreateRewardCard(
                        choice, onChoose, isMerchant ? choice.goldCost : 0, currentGold));
                }
            }

            if (onSkip != null)
            {
                var leaveBtn = new Button(() => onSkip.Invoke()) { text = "Leave" };
                leaveBtn.AddToClassList("merchant-leave-btn");
                _upgradeChoices.Add(leaveBtn);
            }
        }
        EndChoiceScreen();
    }

    public void ShowRoomChoices(int clearedEncounter, List<RoomChoice> choices, Action<RoomChoice> onChoose)
    {
        if (!EnsureUiReady()) return;
        BeginChoiceScreen("Choose Your Path", $"Encounter {clearedEncounter} cleared.",
                          choices != null && choices.Count == 1 ? "ONE PATH AHEAD" : "CHOOSE WHERE TO GO NEXT");

        // Run progress track, inserted between the heading and the cards.
        VisualElement layout = _upgradeChoices?.parent;
        if (layout != null)
        {
            VisualElement track = BuildPathTrack(clearedEncounter);
            layout.Insert(layout.IndexOf(_upgradeChoices), track);
        }

        RoomChoice selected = choices != null && choices.Count > 0 ? choices[0] : null;
        VisualElement selectedCard = null;
        Button enterBtn = null;

        if (_upgradeChoices != null && choices != null)
        {
            foreach (RoomChoice choice in choices)
            {
                VisualElement card = RewardCardBuilder.CreateRoomCard(choice, (c, picked) =>
                {
                    selectedCard?.RemoveFromClassList("reward-card--selected");
                    selected = picked;
                    selectedCard = c;
                    c.AddToClassList("reward-card--selected");
                    if (enterBtn != null) enterBtn.text = $"ENTER {picked.title.ToUpperInvariant()}  >";
                });
                _upgradeChoices.Add(card);
                if (choice == selected)
                {
                    selectedCard = card;
                    card.AddToClassList("reward-card--selected");
                }
            }
        }

        if (_rerollContainer != null && selected != null)
        {
            enterBtn = new Button(() => { if (selected != null) onChoose?.Invoke(selected); })
            {
                text = $"ENTER {selected.title.ToUpperInvariant()}  >"
            };
            enterBtn.AddToClassList("reward-button");
            enterBtn.AddToClassList("loot-take");
            enterBtn.AddToClassList("path-enter");
            _rerollContainer.Add(enterBtn);
        }

        EndChoiceScreen();
    }

    /// <summary>Encounter progress: cleared rooms, the next one, a few ahead, then the next boss.</summary>
    private static VisualElement BuildPathTrack(int cleared)
    {
        var track = new VisualElement { name = "path-track" };
        track.AddToClassList("path-track");

        int boss = (cleared / 10 + 1) * 10;
        var steps = new List<(string label, string mark, string cls)>();
        steps.Add(($"ENC {cleared}", "\u2713", "path-step--done"));
        int enc = cleared + 1;
        bool first = true;
        while (enc < boss && steps.Count < 4)
        {
            steps.Add(($"ENC {enc}", enc.ToString(), first ? "path-step--next" : "path-step--ahead"));
            first = false;
            enc++;
        }
        bool gap = enc < boss;
        steps.Add(("BOSS", "!", boss == cleared + 1 ? "path-step--boss path-step--next" : "path-step--boss"));

        for (int i = 0; i < steps.Count; i++)
        {
            if (i > 0)
            {
                var line = new VisualElement();
                line.AddToClassList("path-line");
                if (i == 1) line.AddToClassList("path-line--lit");
                track.Add(line);
                if (gap && i == steps.Count - 1)
                {
                    var dots = new Label("\u2026");
                    dots.AddToClassList("path-gap");
                    track.Add(dots);
                    var line2 = new VisualElement();
                    line2.AddToClassList("path-line");
                    track.Add(line2);
                }
            }

            var step = new VisualElement();
            step.AddToClassList("path-step");
            foreach (string c in steps[i].cls.Split(' ')) step.AddToClassList(c);

            var dot = new Label(steps[i].mark);
            dot.AddToClassList("path-dot");
            step.Add(dot);
            var lbl = new Label(steps[i].label);
            lbl.AddToClassList("path-label");
            step.Add(lbl);
            track.Add(step);
        }
        return track;
    }

    public void ShowRunSummary(bool won, int reachedEncounter, int gold)
    {
        if (!EnsureUiReady()) return;
        HideOverlays();
        ClearTargetButtons();
        ClearTurnOrder();

        if (_summaryTitle != null) _summaryTitle.text = won ? "Run Complete!" : "Defeated!";

        if (_summaryStats != null)
            _summaryStats.text = $"Reached Encounter {reachedEncounter}   |   {gold} Gold"
                + (RunManager.Instance != null && RunManager.Instance.LastRunWasNewBest ? "   |   New best!" : "");

        RunManager rm = RunManager.Instance;
        var stats = rm?.CurrentRunStats;
        if (_summaryGrid != null)
        {
            _summaryGrid.Clear();
            if (stats != null)
            {
                AddSummaryStat("Damage Dealt", stats.totalDamageDealt);
                AddSummaryStat("Damage Taken", stats.totalDamageTaken);
                AddSummaryStat("Healing Done", stats.healingDone);
                AddSummaryStat("Critical Hits", stats.criticalHitsLanded);
                AddSummaryStat("Enemies Defeated", stats.enemiesDefeated);
                AddSummaryStat("Turns Survived", stats.turnsSurvived);
            }
            AnimateIn(_summaryGrid);
        }

        // Relics and upgrades collected this run (same pills as the HUD strip).
        bool hasLoot = false;
        if (_summaryRelics != null)
        {
            _summaryRelics.Clear();
            if (rm != null)
            {
                foreach (var kvp in rm.EquippedRelics)
                {
                    AddSummaryPill(kvp.Value.relicName, "reward-pill--relic");
                    hasLoot = true;
                }
                foreach (var u in rm.AppliedUpgrades)
                {
                    AddSummaryPill(u.name, IsTrait(u.upgradeType) ? "reward-pill--trait" : "reward-pill--stat");
                    hasLoot = true;
                }
                foreach (var u in rm.AppliedAttackUpgrades)
                {
                    AddSummaryPill(u.name, "reward-pill--attack");
                    hasLoot = true;
                }
            }
        }
        if (_summaryRelicsTitle != null)
            _summaryRelicsTitle.style.display = hasLoot ? DisplayStyle.Flex : DisplayStyle.None;
        if (_summaryRelics != null)
            _summaryRelics.style.display = hasLoot ? DisplayStyle.Flex : DisplayStyle.None;

        SetOverlayOpen(true);
        ShowPanel(_summaryPanel);
    }

    private void AddSummaryStat(string label, int value)
    {
        var cell = new VisualElement();
        cell.AddToClassList("summary-stat");
        var name = new Label(label);
        name.AddToClassList("summary-stat-label");
        var val = new Label(value.ToString());
        val.AddToClassList("summary-stat-value");
        cell.Add(name);
        cell.Add(val);
        _summaryGrid.Add(cell);
    }

    private void AddSummaryPill(string text, string cssClass)
    {
        var pill = new Label(text);
        pill.AddToClassList("reward-pill");
        pill.AddToClassList(cssClass);
        _summaryRelics.Add(pill);
    }

    private void OnRestartClicked()
    {
        RunManager.Instance?.StartRun();
        SceneFader.Load(SceneManager.GetActiveScene().name);
    }
}
