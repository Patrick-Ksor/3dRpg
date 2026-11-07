using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class BattleUIManager : MonoBehaviour
{
    private BaseBattleManager _battleManager;

    [Header("UI Toolkit")]
    [SerializeField] private PanelSettings _panelSettings;
    [SerializeField] private VisualTreeAsset _gameUiAsset;

    private UIDocument _uiDocument;
    private VisualElement _root;
    private Label _messageLabel;
    private Camera _mainCam;

    // Bottom HUD — fixed action zone
    private VisualElement _actionZone;
    private VisualElement _targetPanelZone;
    private Button _attackBtn;
    private Button _specialBtn;
    private Button _itemBtn;
    private Button _defendBtn;
    private bool _buttonsWired;

    // Left stats panel
    private VisualElement _statsPanel;
    private VisualElement _statsPanelPortrait;
    private Label _statsPanelName;
    private ProgressBar _statsPanelHpBar;
    private ProgressBar _statsPanelMpBar;
    private VisualElement _statsPanelRows;

    // Run HUD
    private VisualElement _runHud;
    private Label _encounterLabel;
    private Label _goldLabel;
    private VisualElement _goldIcon;

    // Run rewards strip
    private VisualElement _runRewards;

    // Intermission overlay
    private VisualElement _intermissionPanel;
    private Label _intermissionTitle;
    private Label _intermissionGold;
    private VisualElement _upgradeChoices;
    private VisualElement _rerollContainer;

    // Run-summary overlay
    private VisualElement _summaryPanel;
    private Label _summaryTitle;
    private Label _summaryStats;

    // Battle log
    private ScrollView _battleLogScroll;
    private VisualElement _battleLogContent;
    private const int MaxLogEntries = 25;

    public enum LogEntryType { Normal, Crit, Heal, Death, System }

    private const string MessageLabelName = "battle-message";
    private const string ActionZoneName = "action-zone";
    private const string TargetZoneName = "target-panel-zone";
    private const string RunHudName = "run-hud";
    private const string EncounterLabelName = "encounter-label";
    private const string GoldLabelName = "gold-label";
    private const string IntermissionPanelName = "intermission-panel";
    private const string IntermissionTitleName = "intermission-title";
    private const string IntermissionGoldName = "intermission-gold";
    private const string UpgradeChoicesName = "upgrade-choices";
    private const string RunRewardsName = "run-rewards";
    private const string SummaryPanelName = "run-summary-panel";
    private const string SummaryTitleName = "summary-title";
    private const string SummaryStatsName = "summary-stats";

    public void Initialize(BaseBattleManager battleManager)
    {
        _battleManager = battleManager;
        _mainCam = Camera.main;

        EnsureDocument();
        CacheUiReferences();
    }

    public void SetupHealthBars(List<Unit> allUnits)
    {
        // Health display is handled by FloatingHealthBar components on each unit's GameObject.
        // Refresh the player card immediately so it shows correct values from the new encounter.
        Unit player = allUnits?.Find(u => u.isPlayer);
        if (player != null) UpdatePlayerCard(player);
    }

    public void UpdateHealth(Unit unit)
    {
        if (!EnsureUiReady() || !unit.isPlayer) return;
        UpdatePlayerCard(unit);
    }

    public void UpdateStats(Unit unit)
    {
        // Stat display is handled by the intermission rewards strip; no in-battle chips.
    }

    // ─── PLAYER CARD ──────────────────────────────────────────────────
    private void UpdatePlayerCard(Unit u)
    {
        if (u == null) return;

        // Left stats panel
        if (_statsPanelName != null) _statsPanelName.text = u.unitName;
        if (_statsPanelPortrait != null && u.portrait != null)
            _statsPanelPortrait.style.backgroundImage = new StyleBackground(u.portrait);
        if (_statsPanelHpBar != null) SetProgressBar(_statsPanelHpBar, u.currentHealth, u.maxHealth);
        if (_statsPanelMpBar != null)
        {
            _statsPanelMpBar.style.display = u.maxMana > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (u.maxMana > 0) SetProgressBar(_statsPanelMpBar, u.currentMana, u.maxMana);
        }
        if (_statsPanelRows != null)
        {
            _statsPanelRows.Clear();
            _statsPanelRows.Add(CreateStatRow("Sword.png", "ATK", u.attack.ToString()));
            _statsPanelRows.Add(CreateStatRow("Shield.png", "DEF", u.defense.ToString()));
            _statsPanelRows.Add(CreateStatRow("Energy.png", "SPD", u.speed.ToString()));
            _statsPanelRows.Add(CreateStatRow("Clover.png", "LCK", u.luck.ToString()));
        }
    }

    // ─── COMMAND MENU (fixed 4-button bar) ────────────────────────────────────
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

        if (_attackBtn != null) _attackBtn.SetEnabled(true);
        if (_specialBtn != null) _specialBtn.SetEnabled(hasSpecials);
        if (_itemBtn != null) _itemBtn.SetEnabled(hasItems);
        if (_defendBtn != null) _defendBtn.SetEnabled(true);
    }

    public void HideCommandMenu()
    {
        if (!EnsureUiReady()) return;
        _targetPanelZone?.Clear();
        if (_targetPanelZone != null) _targetPanelZone.style.display = DisplayStyle.None;
        if (_actionZone != null) _actionZone.style.display = DisplayStyle.None;
    }

    // ─── PERSISTENT ACTION BUTTON HANDLERS ───────────────────────────────────
    private void HandleAttackClicked()
    {
        if (_battleManager == null) return;
        Unit activeUnit = _battleManager.GetActiveUnit();
        if (activeUnit == null) return;
        if (activeUnit.basicAttack != null)
        {
            if (activeUnit.basicAttack.isAoE)
            {
                var aliveEnemies = _battleManager.GetAliveEnemies();
                if (aliveEnemies.Count > 0)
                    _battleManager.PlayerSelectTarget(aliveEnemies[0], activeUnit.basicAttack);
            }
            else
                ShowTargetSelection(activeUnit.basicAttack, _battleManager.GetAliveEnemies());
        }
        else
            SetMessage("No basic attack assigned!");
    }

    private void HandleSpecialClicked()
    {
        if (_battleManager == null) return;
        Unit activeUnit = _battleManager.GetActiveUnit();
        if (activeUnit != null) ShowSpecialSelection(activeUnit);
    }

    private void HandleItemClicked()
    {
        if (_battleManager == null) return;
        Unit activeUnit = _battleManager.GetActiveUnit();
        if (activeUnit != null) ShowItemSelection(activeUnit);
    }

    private void HandleDefendClicked() => _battleManager?.PlayerDefend();

    // ─── SUB-MENU HELPERS ──────────────────────────────────────────────────
    private void OpenSubMenu(System.Action populate)
    {
        if (_actionZone != null) _actionZone.style.display = DisplayStyle.None;
        if (_targetPanelZone != null) _targetPanelZone.style.display = DisplayStyle.Flex;
        _targetPanelZone?.Clear();
        populate();
    }

    private void SpawnMenuButton(string label, bool interactable, Action onClick, string iconFile = null)
    {
        if (!EnsureUiReady() || _targetPanelZone == null) return;

        Button btn = new Button(() => onClick?.Invoke());
        btn.SetEnabled(interactable);
        btn.AddToClassList("menu-button");

        Sprite icon = iconFile != null ? LoadColoredIcon(iconFile) : null;
        if (icon != null)
        {
            btn.AddToClassList("menu-button--has-icon");
            Image img = new Image { sprite = icon };
            img.AddToClassList("menu-button-icon");
            btn.Add(img);
            btn.Add(new Label(label));
        }
        else
        {
            btn.text = label;
        }
        _targetPanelZone.Add(btn);
    }

    // ─── SPECIAL ATTACK SUBMENU ───
    private void ShowSpecialSelection(Unit activeUnit)
    {
        OpenSubMenu(() =>
        {
            foreach (var attack in activeUnit.specialAttacks)
            {
                if (attack == null) continue;
                bool canAfford = activeUnit.currentMana >= attack.manaCost;
                string costStr = attack.manaCost > 0 ? $" ({attack.manaCost} MP)" : "";
                AttackAction capturedAttack = attack;

                SpawnMenuButton(attack.actionName + costStr, canAfford, () =>
                {
                    _targetPanelZone?.Clear();
                    if (capturedAttack.isAoE)
                    {
                        var aliveEnemies = _battleManager.GetAliveEnemies();
                        if (aliveEnemies.Count > 0)
                            _battleManager.PlayerSelectTarget(aliveEnemies[0], capturedAttack);
                    }
                    else
                        ShowTargetSelection(capturedAttack, _battleManager.GetAliveEnemies());
                });
            }
            SpawnMenuButton("Back", true, () => ShowCommandMenu());
        });
    }

    // ─── ITEM SUBMENU ───
    private void ShowItemSelection(Unit activeUnit)
    {
        OpenSubMenu(() =>
        {
            var grouped = new Dictionary<BattleItem, int>();
            foreach (var item in activeUnit.inventory)
            {
                if (item == null) continue;
                if (!grouped.ContainsKey(item)) grouped[item] = 0;
                grouped[item]++;
            }

            foreach (var kvp in grouped)
            {
                BattleItem capturedItem = kvp.Key;
                SpawnMenuButton($"{capturedItem.itemName} x{kvp.Value}", true, () =>
                {
                    _targetPanelZone?.Clear();
                    _battleManager.PlayerUseItem(capturedItem);
                });
            }

            if (grouped.Count == 0)
                SpawnMenuButton("No items!", false, null);

            SpawnMenuButton("Back", true, () => ShowCommandMenu());
        });
    }

    // ─── TARGET SELECTION ───
    private void ShowAttackSelection(Unit activeUnit)
    {
        ShowCommandMenu();
    }

    private void ShowTargetSelection(AttackAction selectedAction, List<Unit> enemies)
    {
        OpenSubMenu(() =>
        {
            SetMessage($"Select target for {selectedAction.actionName}");
            foreach (var enemy in enemies)
            {
                Unit capturedTarget = enemy;
                SpawnMenuButton(enemy.unitName, true, () =>
                {
                    _targetPanelZone?.Clear();
                    _battleManager.PlayerSelectTarget(capturedTarget, selectedAction);
                });
            }
            SpawnMenuButton("Back", true, () => ShowCommandMenu());
        });
    }

    public void ClearTargetButtons()
    {
        if (!EnsureUiReady()) return;
        _targetPanelZone?.Clear();
        if (_targetPanelZone != null) _targetPanelZone.style.display = DisplayStyle.None;
    }

    public void SetMessage(string msg)
    {
        if (!EnsureUiReady()) return;

        if (_messageLabel != null)
        {
            _messageLabel.text = msg;
            _messageLabel.style.display = string.IsNullOrEmpty(msg) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        if (!string.IsNullOrEmpty(msg))
        {
            LogEntryType logType = LogEntryType.Normal;
            string lower = msg.ToLowerInvariant();
            if (lower.Contains("critical")) logType = LogEntryType.Crit;
            else if (lower.Contains("dies") || lower.Contains("defeated") || lower.Contains("dead")) logType = LogEntryType.Death;
            else if (lower.Contains("regenerates") || lower.Contains("heals") || lower.Contains("restored") || lower.Contains("burns for")) logType = LogEntryType.Heal;
            else if (lower.Contains("encounter") || lower.Contains("fight!") || lower.Contains("battle")) logType = LogEntryType.System;
            if (lower.Contains("burns for")) logType = LogEntryType.Death;
            AddLogEntry(msg, logType);
        }
    }

    /// <summary>Clears all entries from the battle log.</summary>
    public void ClearBattleLog()
    {
        _battleLogContent?.Clear();
    }

    /// <summary>Adds an entry to the battle log panel (newest at top).</summary>
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

        // Cap entries
        while (_battleLogContent.childCount > MaxLogEntries)
            _battleLogContent.RemoveAt(_battleLogContent.childCount - 1);
    }

    public void EnableAttackButton(bool enable)
    {
        if (!EnsureUiReady()) return;

        if (enable)
        {
            ShowCommandMenu();
        }
        else
        {
            _targetPanelZone?.Clear();
            if (_targetPanelZone != null) _targetPanelZone.style.display = DisplayStyle.None;
            if (_actionZone != null) _actionZone.style.display = DisplayStyle.Flex;
            if (_attackBtn != null) _attackBtn.SetEnabled(false);
            if (_specialBtn != null) _specialBtn.SetEnabled(false);
            if (_itemBtn != null) _itemBtn.SetEnabled(false);
            if (_defendBtn != null) _defendBtn.SetEnabled(false);
        }
    }

    private bool EnsureUiReady()
    {
        if (_uiDocument == null)
        {
            EnsureDocument();
        }

        if (_root == null || _actionZone == null || _messageLabel == null)
        {
            CacheUiReferences();
        }

        return _root != null && _actionZone != null && _messageLabel != null;
    }

    private void EnsureDocument()
    {
        _uiDocument = GetComponent<UIDocument>();
        if (_uiDocument == null)
        {
            _uiDocument = gameObject.AddComponent<UIDocument>();
        }

        if (_uiDocument.panelSettings == null)
        {
            _uiDocument.panelSettings = _panelSettings != null
                ? _panelSettings
                : Resources.Load<PanelSettings>("UI/PanelSettings");

#if UNITY_EDITOR
            if (_uiDocument.panelSettings == null)
            {
                _uiDocument.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI Toolkit/PanelSettings.asset");
            }
#endif

            if (_uiDocument.panelSettings == null)
            {
                _uiDocument.panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            }
        }

        if (_uiDocument.visualTreeAsset == null)
        {
            if (_gameUiAsset != null)
            {
                _uiDocument.visualTreeAsset = _gameUiAsset;
            }
            else
            {
                _uiDocument.visualTreeAsset = Resources.Load<VisualTreeAsset>("UI/GameUi");
#if UNITY_EDITOR
                if (_uiDocument.visualTreeAsset == null)
                {
                    _uiDocument.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/GameUi.uxml");
                }
#endif
            }
        }
    }

    private void CacheUiReferences()
    {
        _root = _uiDocument != null ? _uiDocument.rootVisualElement : null;
        if (_root == null) return;

        _actionZone = _root.Q<VisualElement>(ActionZoneName);
        _targetPanelZone = _root.Q<VisualElement>(TargetZoneName);
        _messageLabel = _root.Q<Label>(MessageLabelName);

        if (_actionZone == null || _messageLabel == null)
        {
            // If root has no children the UIDocument hasn't built its tree yet —
            // don't overwrite with the fallback; EnsureUiReady will retry on the next call.
            if (_root.childCount == 0) return;
            BuildFallbackTree();
            return;
        }

        // Left stats panel
        _statsPanel = _root.Q<VisualElement>("stats-panel-left");
        _statsPanelPortrait = _root.Q<VisualElement>("stats-panel-portrait");
        _statsPanelName = _root.Q<Label>("stats-panel-name");
        _statsPanelHpBar = _root.Q<ProgressBar>("stats-panel-hp");
        _statsPanelMpBar = _root.Q<ProgressBar>("stats-panel-mp");
        _statsPanelRows = _root.Q<VisualElement>("stats-panel-rows");

        // Action buttons
        _attackBtn = _root.Q<Button>("attack-btn");
        _specialBtn = _root.Q<Button>("special-btn");
        _defendBtn = _root.Q<Button>("defend-btn");
        _itemBtn = _root.Q<Button>("item-btn");

        // Wire icons once
        if (!_buttonsWired)
        {
            WireActionButtonIcon(_attackBtn, "AttackButton.png");
            WireActionButtonIcon(_specialBtn, "Energy.png");
            WireActionButtonIcon(_defendBtn, "shield blue.png");
            WireActionButtonIcon(_itemBtn, "Bag.png");

            if (_attackBtn != null) _attackBtn.clicked += HandleAttackClicked;
            if (_specialBtn != null) _specialBtn.clicked += HandleSpecialClicked;
            if (_defendBtn != null) _defendBtn.clicked += HandleDefendClicked;
            if (_itemBtn != null) _itemBtn.clicked += HandleItemClicked;
            _buttonsWired = true;
        }

        // Start with target zone hidden
        if (_targetPanelZone != null) _targetPanelZone.style.display = DisplayStyle.None;

        // Optional run-HUD elements (present in UXML)
        _runHud = _root.Q<VisualElement>(RunHudName);
        _encounterLabel = _root.Q<Label>(EncounterLabelName);
        _goldLabel = _root.Q<Label>(GoldLabelName);
        _goldIcon = _root.Q<VisualElement>("gold-icon");
        if (_goldIcon != null) { Sprite s = LoadColoredIcon("Coin.png"); if (s != null) _goldIcon.style.backgroundImage = new StyleBackground(s); }

        // Run rewards strip
        _runRewards = _root.Q<VisualElement>(RunRewardsName);

        // Reroll container (inside intermission)
        _rerollContainer = _root.Q<VisualElement>("reroll-container");

        // Battle log
        _battleLogScroll = _root.Q<ScrollView>("battle-log-scroll");
        _battleLogContent = _root.Q<VisualElement>("battle-log-content");

        // Overlay elements
        _intermissionPanel = _root.Q<VisualElement>(IntermissionPanelName);
        _intermissionTitle = _root.Q<Label>(IntermissionTitleName);
        _intermissionGold = _root.Q<Label>(IntermissionGoldName);
        _upgradeChoices = _root.Q<VisualElement>(UpgradeChoicesName);
        _summaryPanel = _root.Q<VisualElement>(SummaryPanelName);
        _summaryTitle = _root.Q<Label>(SummaryTitleName);
        _summaryStats = _root.Q<Label>(SummaryStatsName);

        // Ensure overlays start hidden
        if (_intermissionPanel != null) _intermissionPanel.style.display = DisplayStyle.None;
        if (_summaryPanel != null) _summaryPanel.style.display = DisplayStyle.None;
    }

    private void WireActionButtonIcon(Button btn, string iconFile)
    {
        if (btn == null) return;
        Sprite icon = LoadColoredIcon(iconFile);
        if (icon == null) return;
        var img = btn.Q<VisualElement>(className: "action-btn-icon");
        if (img != null) img.style.backgroundImage = new StyleBackground(icon);
    }

    private void BuildFallbackTree()
    {
        if (_root == null) return;
        _root.Clear();

        // Run HUD (top)
        _runHud = new VisualElement { name = RunHudName };
        _runHud.AddToClassList("run-hud");
        _encounterLabel = new Label("Encounter 1") { name = EncounterLabelName };
        _encounterLabel.AddToClassList("run-hud-label");
        _goldLabel = new Label("0 Gold") { name = GoldLabelName };
        _goldLabel.AddToClassList("run-hud-label");
        _goldLabel.AddToClassList("gold-label");
        _runHud.Add(_encounterLabel);
        _runHud.Add(_goldLabel);
        _root.Add(_runHud);

        // Run rewards strip
        _runRewards = new VisualElement { name = RunRewardsName };
        _runRewards.AddToClassList("run-rewards");
        _root.Add(_runRewards);

        // Left stats panel
        _statsPanel = new VisualElement { name = "stats-panel-left" };
        _statsPanel.AddToClassList("stats-panel-left");
        _statsPanelPortrait = new VisualElement { name = "stats-panel-portrait" };
        _statsPanelPortrait.AddToClassList("stats-panel-portrait");
        _statsPanel.Add(_statsPanelPortrait);
        _statsPanelName = new Label("") { name = "stats-panel-name" };
        _statsPanelName.AddToClassList("stats-panel-name");
        _statsPanel.Add(_statsPanelName);
        _statsPanelHpBar = new ProgressBar { name = "stats-panel-hp" };
        _statsPanelHpBar.AddToClassList("stats-panel-hp");
        _statsPanelHpBar.AddToClassList("health-player");
        _statsPanel.Add(_statsPanelHpBar);
        _statsPanelMpBar = new ProgressBar { name = "stats-panel-mp" };
        _statsPanelMpBar.AddToClassList("stats-panel-mp");
        _statsPanelMpBar.style.display = DisplayStyle.None;
        _statsPanel.Add(_statsPanelMpBar);
        _statsPanelRows = new VisualElement { name = "stats-panel-rows" };
        _statsPanelRows.AddToClassList("stats-panel-rows");
        _statsPanel.Add(_statsPanelRows);
        _root.Add(_statsPanel);

        // Bottom HUD: action-zone | target-panel-zone
        VisualElement bottomHud = new VisualElement();
        bottomHud.AddToClassList("bottom-hud");

        // Action zone (4 fixed buttons)
        _actionZone = new VisualElement { name = ActionZoneName };
        _actionZone.AddToClassList("action-zone");
        _actionZone.style.flexDirection = FlexDirection.Row;
        _attackBtn = CreateActionButton("attack-btn", "Attack");
        _specialBtn = CreateActionButton("special-btn", "Special");
        _defendBtn = CreateActionButton("defend-btn", "Defend");
        _itemBtn = CreateActionButton("item-btn", "Items");
        _actionZone.Add(_attackBtn);
        _actionZone.Add(_specialBtn);
        _actionZone.Add(_defendBtn);
        _actionZone.Add(_itemBtn);
        bottomHud.Add(_actionZone);

        // Target panel zone (sub-menu overlay)
        _targetPanelZone = new VisualElement { name = TargetZoneName };
        _targetPanelZone.AddToClassList("target-panel-zone");
        _targetPanelZone.style.display = DisplayStyle.None;
        bottomHud.Add(_targetPanelZone);

        _root.Add(bottomHud);

        // Message zone — absolutely positioned above the bottom HUD
        VisualElement msgZone = new VisualElement { name = "bottom-message-zone" };
        msgZone.AddToClassList("bottom-message-zone");
        _messageLabel = new Label("") { name = MessageLabelName };
        _messageLabel.AddToClassList("battle-message");
        _messageLabel.style.display = DisplayStyle.None;
        msgZone.Add(_messageLabel);
        _root.Add(msgZone);

        // Battle log — absolute, right side
        _battleLogScroll = new ScrollView { name = "battle-log-scroll" };
        _battleLogScroll.AddToClassList("battle-log-scroll");
        _battleLogContent = new VisualElement { name = "battle-log-content" };
        _battleLogContent.AddToClassList("battle-log-content");
        _battleLogScroll.Add(_battleLogContent);
        _root.Add(_battleLogScroll);

        // Wire buttons
        if (!_buttonsWired)
        {
            WireActionButtonIcon(_attackBtn, "AttackButton.png");
            WireActionButtonIcon(_specialBtn, "Energy.png");
            WireActionButtonIcon(_defendBtn, "shield blue.png");
            WireActionButtonIcon(_itemBtn, "Bag.png");

            _attackBtn.clicked += HandleAttackClicked;
            _specialBtn.clicked += HandleSpecialClicked;
            _defendBtn.clicked += HandleDefendClicked;
            _itemBtn.clicked += HandleItemClicked;
            _buttonsWired = true;
        }

        // Intermission overlay
        _intermissionPanel = new VisualElement { name = IntermissionPanelName };
        _intermissionPanel.AddToClassList("overlay-panel");
        _intermissionPanel.style.display = DisplayStyle.None;
        var intermCard = new VisualElement();
        intermCard.AddToClassList("intermission-layout");
        _intermissionTitle = new Label("Victory!") { name = IntermissionTitleName };
        _intermissionTitle.AddToClassList("overlay-title");
        _intermissionGold = new Label("+0 Gold") { name = IntermissionGoldName };
        _intermissionGold.AddToClassList("overlay-subtitle");
        var chooseLabel = new Label("Choose an upgrade:");
        chooseLabel.AddToClassList("overlay-instruction");
        _upgradeChoices = new VisualElement { name = UpgradeChoicesName };
        _upgradeChoices.AddToClassList("upgrade-choices");
        _rerollContainer = new VisualElement();
        _rerollContainer.AddToClassList("reroll-container");
        intermCard.Add(_intermissionTitle);
        intermCard.Add(_intermissionGold);
        intermCard.Add(chooseLabel);
        intermCard.Add(_upgradeChoices);
        intermCard.Add(_rerollContainer);
        _intermissionPanel.Add(intermCard);
        _root.Add(_intermissionPanel);

        // Run-summary overlay
        _summaryPanel = new VisualElement { name = SummaryPanelName };
        _summaryPanel.AddToClassList("overlay-panel");
        _summaryPanel.style.display = DisplayStyle.None;
        var summaryCard = new VisualElement();
        summaryCard.AddToClassList("overlay-card");
        _summaryTitle = new Label("Run Complete!") { name = SummaryTitleName };
        _summaryTitle.AddToClassList("overlay-title");
        _summaryStats = new Label("") { name = SummaryStatsName };
        _summaryStats.AddToClassList("overlay-subtitle");
        var restartBtn = new Button { name = "summary-restart", text = "New Run" };
        restartBtn.AddToClassList("menu-button");
        restartBtn.AddToClassList("restart-button");
        restartBtn.clicked += OnRestartClicked;
        summaryCard.Add(_summaryTitle);
        summaryCard.Add(_summaryStats);
        summaryCard.Add(restartBtn);
        _summaryPanel.Add(summaryCard);
        _root.Add(_summaryPanel);
    }

    private static Button CreateActionButton(string name, string label)
    {
        var btn = new Button { name = name };
        btn.AddToClassList("action-btn");
        var icon = new VisualElement();
        icon.AddToClassList("action-btn-icon");
        var lbl = new Label(label);
        lbl.AddToClassList("action-btn-label");
        btn.Add(icon);
        btn.Add(lbl);
        return btn;
    }

    private static void SetProgressBar(ProgressBar progressBar, float current, float max)
    {
        if (progressBar == null) return;

        float safeMax = Mathf.Max(1f, max);
        float clampedCurrent = Mathf.Clamp(current, 0f, safeMax);
        float normalized = clampedCurrent / safeMax;

        progressBar.lowValue = 0f;
        progressBar.highValue = 100f;
        progressBar.value = normalized * 100f;
        progressBar.title = $"{Mathf.RoundToInt(clampedCurrent)}/{Mathf.RoundToInt(safeMax)}";

        VisualElement fill = progressBar.Q(className: "unity-progress-bar__progress");
        if (fill != null)
        {
            fill.style.opacity = normalized > 0.0001f ? 1f : 0f;
        }
    }

    private static string SanitizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unit";
        return value.Replace(" ", "-").ToLowerInvariant();
    }

#if UNITY_EDITOR
    private static Sprite LoadColoredIcon(string fileName) =>
        AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Violet Theme Ui/Colored Icons/{fileName}");
#else
    private static Sprite LoadColoredIcon(string fileName) => null;
#endif

    private static Sprite LoadRewardIcon(string resourceName) =>
        Resources.Load<Sprite>($"Rewards/{resourceName}");

    private static string GetChoiceTypeLabel(RewardChoice choice)
    {
        if (choice.curseEffect != null) return "CURSE";
        if (choice.legendaryRelic != null) return "RELIC";
        if (choice.statUpgrade != null)
            return choice.statUpgrade.upgradeType switch
            {
                UpgradeType.Vampiric => "TRAIT",
                UpgradeType.Thorns => "TRAIT",
                UpgradeType.Regenerating => "TRAIT",
                UpgradeType.Enraged => "TRAIT",
                _ => "UPGRADE",
            };
        if (choice.attackUpgrade != null)
            return choice.attackUpgrade.upgradeType == AttackUpgradeType.UnlockNewAttack
                ? "NEW SKILL" : "ATTACK UP";
        return "REWARD";
    }

    private static Sprite GetRewardIcon(RewardChoice choice)
    {
        // Relic icon from the SO asset itself
        if (choice.legendaryRelic != null && choice.legendaryRelic.icon != null)
            return choice.legendaryRelic.icon;

        // Step 1: attack-specific icon (attack name, spaces stripped)
        if (choice.attackUpgrade != null && !string.IsNullOrEmpty(choice.attackUpgrade.targetAttackName))
        {
            Sprite s = LoadRewardIcon(choice.attackUpgrade.targetAttackName.Replace(" ", ""));
            if (s != null) return s;
        }

        // Step 2: type fallback
        if (choice.statUpgrade != null)
        {
            string key = choice.statUpgrade.upgradeType switch
            {
                UpgradeType.MaxHealth => "MaxHealth",
                UpgradeType.Attack => "Attack",
                UpgradeType.Defense => "Defense",
                UpgradeType.Speed => "Speed",
                UpgradeType.MaxMana => "MaxMana",
                UpgradeType.Luck => "Luck",
                UpgradeType.Vampiric => "Vampiric",
                UpgradeType.Thorns => "Thorns",
                UpgradeType.Regenerating => "Regenerating",
                UpgradeType.Enraged => "Enraged",
                _ => null,
            };
            if (key != null) return LoadRewardIcon(key);
        }

        if (choice.attackUpgrade != null)
        {
            string key = choice.attackUpgrade.upgradeType switch
            {
                AttackUpgradeType.DamageBoost => "Damage",
                AttackUpgradeType.ManaCostReduce => "ManaCost",
                AttackUpgradeType.AoEUnlock => "AoE",
                AttackUpgradeType.ProjectileCountBoost => "Projectile",
                AttackUpgradeType.SpreadAngleBoost => "Spread",
                AttackUpgradeType.UnlockNewAttack => "NewAttack",
                AttackUpgradeType.HitCountBoost => "MultiHit",
                _ => null,
            };
            if (key != null) return LoadRewardIcon(key);
        }

        return null;
    }

    private static VisualElement CreateStatRow(string iconFile, string labelText, string value)
    {
        VisualElement row = new VisualElement();
        row.AddToClassList("stats-panel-row");
        Sprite icon = LoadColoredIcon(iconFile);
        if (icon != null)
        {
            Image img = new Image { sprite = icon };
            img.AddToClassList("stats-panel-row-icon");
            row.Add(img);
        }
        Label lbl = new Label(labelText);
        lbl.AddToClassList("stats-panel-row-label");
        row.Add(lbl);
        Label val = new Label(value);
        val.AddToClassList("stats-panel-row-value");
        row.Add(val);
        return row;
    }

    private static VisualElement CreateStatChip(string iconFile, string value, string chipName)
    {
        VisualElement chip = new VisualElement();
        chip.name = chipName;
        chip.AddToClassList("stat-chip");
        Sprite icon = LoadColoredIcon(iconFile);
        if (icon != null)
        {
            Image img = new Image { sprite = icon };
            img.AddToClassList("stat-chip-icon");
            chip.Add(img);
        }
        Label lbl = new Label(value);
        lbl.AddToClassList("stat-chip-value");
        chip.Add(lbl);
        return chip;
    }

    private static Label GetChipLabel(VisualElement chip) =>
        chip.Q<Label>(className: "stat-chip-value") ?? chip as Label;

    private static string AffixTypeToModifierClass(EnemyAffixType type) => type switch
    {
        EnemyAffixType.Berserker => "berserker",
        EnemyAffixType.Armored => "armored",
        EnemyAffixType.Swift => "swift",
        EnemyAffixType.Bulwark => "bulwark",
        EnemyAffixType.Regenerating => "regenerating",
        EnemyAffixType.Thorns => "thorns",
        EnemyAffixType.Vampiric => "vampiric",
        EnemyAffixType.Enraged => "enraged",
        _ => "berserker",
    };

    public void ShowRestartMenu()
    {
        // Fallback path: show a generic run summary
        ShowRunSummary(false, 1, 0);
    }

    // ─── RUN HUD & OVERLAYS ───────────────────────────────────────────────────

    public void UpdateRunHud(int current, int gold)
    {
        if (!EnsureUiReady()) return;
        if (_encounterLabel != null) _encounterLabel.text = $"Encounter {current}";
        if (_goldLabel != null) _goldLabel.text = $"{gold} Gold";
        RefreshRunRewards();
    }

    public void RefreshRunRewards()
    {
        if (_runRewards == null) return;
        RunManager rm = RunManager.Instance;
        if (rm == null) { _runRewards.Clear(); return; }

        // Tally all chosen upgrades by display name, preserving category for coloring
        var entries = new Dictionary<string, (int count, string css)>();

        foreach (var u in rm.AppliedUpgrades)
        {
            bool isTrait = u.upgradeType == UpgradeType.Vampiric ||
                           u.upgradeType == UpgradeType.Thorns ||
                           u.upgradeType == UpgradeType.Regenerating ||
                           u.upgradeType == UpgradeType.Enraged;
            string css = isTrait ? "reward-pill--trait" : "reward-pill--stat";
            if (!entries.ContainsKey(u.name)) entries[u.name] = (0, css);
            entries[u.name] = (entries[u.name].count + 1, css);
        }
        foreach (var u in rm.AppliedAttackUpgrades)
        {
            if (!entries.ContainsKey(u.name)) entries[u.name] = (0, "reward-pill--attack");
            entries[u.name] = (entries[u.name].count + 1, "reward-pill--attack");
        }

        _runRewards.Clear();
        // Relic pills — shown first with a distinct gold style,
        // followed by individual trait/stat pills for each relic's boosts.
        foreach (var kvp in rm.EquippedRelics)
        {
            var pill = new Label($"\u2605 {kvp.Value.relicName}");
            pill.AddToClassList("reward-pill");
            pill.AddToClassList("reward-pill--relic");
            _runRewards.Add(pill);

            // Show each stat/trait boost granted by this relic as its own pill
            foreach (var boost in kvp.Value.statBoosts)
            {
                if (!entries.ContainsKey(boost.name))
                {
                    bool isTrait = boost.upgradeType == UpgradeType.Vampiric ||
                                   boost.upgradeType == UpgradeType.Thorns ||
                                   boost.upgradeType == UpgradeType.Regenerating ||
                                   boost.upgradeType == UpgradeType.Enraged;
                    string css = isTrait ? "reward-pill--trait" : "reward-pill--stat";
                    entries[boost.name] = (1, css);
                }
            }
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

    public void HideOverlays()
    {
        if (_intermissionPanel != null) _intermissionPanel.style.display = DisplayStyle.None;
        if (_summaryPanel != null) _summaryPanel.style.display = DisplayStyle.None;
    }

    public void ShowIntermission(int clearedEncounter, int goldEarned, List<RewardChoice> choices, Action<RewardChoice> onChoose, Action onReroll = null)
    {
        if (!EnsureUiReady()) return;
        HideOverlays();
        ClearTargetButtons();

        if (_intermissionTitle != null)
            _intermissionTitle.text = $"Encounter {clearedEncounter} Clear!";
        if (_intermissionGold != null)
            _intermissionGold.text = $"+{goldEarned} Gold";

        if (_upgradeChoices != null)
        {
            _upgradeChoices.Clear();
            foreach (var choice in choices)
            {
                var captured = choice;
                var card = new VisualElement();
                card.AddToClassList("reward-card");

                if (captured.legendaryRelic != null)
                    card.AddToClassList("reward-card--relic");

                bool isLegendary = captured.statUpgrade != null &&
                    (captured.statUpgrade.upgradeType == UpgradeType.Vampiric ||
                     captured.statUpgrade.upgradeType == UpgradeType.Thorns ||
                     captured.statUpgrade.upgradeType == UpgradeType.Regenerating ||
                     captured.statUpgrade.upgradeType == UpgradeType.Enraged);
                if (isLegendary)
                    card.AddToClassList("reward-card--legendary");

                if (captured.curseEffect != null)
                    card.AddToClassList("reward-card--curse");

                // Category header strip
                var typeHeader = new VisualElement();
                typeHeader.AddToClassList("reward-card-type");
                var typeLabel = new Label(GetChoiceTypeLabel(captured));
                typeLabel.AddToClassList("reward-card-type-label");
                typeHeader.Add(typeLabel);
                card.Add(typeHeader);

                // Body (icon + name + desc)
                var body = new VisualElement();
                body.AddToClassList("reward-card-body");

                Sprite icon = GetRewardIcon(captured);
                if (icon != null)
                {
                    var img = new Image { sprite = icon };
                    img.AddToClassList("reward-card-icon");
                    body.Add(img);
                }

                var nameLabel = new Label(captured.name);
                nameLabel.AddToClassList("reward-card-name");
                body.Add(nameLabel);

                var descLabel = new Label(captured.description);
                descLabel.AddToClassList("reward-card-desc");
                body.Add(descLabel);

                if (captured.legendaryRelic != null && !string.IsNullOrEmpty(captured.targetUnitName))
                {
                    var forLabel = new Label($"\u2605 For: {captured.targetUnitName}");
                    forLabel.AddToClassList("reward-card-for");
                    body.Add(forLabel);
                }

                card.Add(body);

                var selectBtn = new Button(() => onChoose?.Invoke(captured)) { text = "SELECT" };
                selectBtn.AddToClassList("reward-button");
                card.Add(selectBtn);

                _upgradeChoices.Add(card);
            }

            // Reroll button — sits below the cards row
            _rerollContainer?.Clear();
            if (onReroll != null)
            {
                var rerollBtn = new Button();
                rerollBtn.text = "\ud83d\udd00 Reroll (1 left)";
                rerollBtn.AddToClassList("menu-button");
                rerollBtn.AddToClassList("reroll-button");
                rerollBtn.RegisterCallback<ClickEvent>(_ =>
                {
                    onReroll.Invoke();
                    rerollBtn.SetEnabled(false);
                    rerollBtn.text = "Rerolled";
                });
                _rerollContainer?.Add(rerollBtn);
            }
        }

        if (_intermissionPanel != null)
            _intermissionPanel.style.display = DisplayStyle.Flex;
    }

    /// <summary>Shows the intermission panel pre-populated for a non-combat event encounter.</summary>
    public void ShowEventOverlay(string title, string subtitle, List<RewardChoice> choices, Action<RewardChoice> onChoose, int currentGold = -1, Action onSkip = null)
    {
        if (!EnsureUiReady()) return;
        HideOverlays();
        ClearTargetButtons();

        if (_intermissionTitle != null) _intermissionTitle.text = title;
        if (_intermissionGold != null) _intermissionGold.text = subtitle;

        bool isMerchant = choices != null && choices.Count > 0 && choices[0].goldCost > 0;

        if (_upgradeChoices != null)
        {
            _upgradeChoices.Clear();
            _rerollContainer?.Clear(); // prevent stale reroll button from prior intermission
            foreach (var choice in choices)
            {
                var captured = choice;
                var card = new VisualElement();
                card.AddToClassList("reward-card");

                if (captured.legendaryRelic != null)
                    card.AddToClassList("reward-card--relic");

                if (captured.curseEffect != null)
                    card.AddToClassList("reward-card--curse");

                // Category header strip
                var typeHeader = new VisualElement();
                typeHeader.AddToClassList("reward-card-type");
                var typeLabel = new Label(GetChoiceTypeLabel(captured));
                typeLabel.AddToClassList("reward-card-type-label");
                typeHeader.Add(typeLabel);
                card.Add(typeHeader);

                // Body (icon + name + desc)
                var body = new VisualElement();
                body.AddToClassList("reward-card-body");

                Sprite icon = GetRewardIcon(captured);
                if (icon != null)
                {
                    var img = new Image { sprite = icon };
                    img.AddToClassList("reward-card-icon");
                    body.Add(img);
                }

                var nameLabel = new Label(captured.name);
                nameLabel.AddToClassList("reward-card-name");
                body.Add(nameLabel);

                var descLabel = new Label(captured.description);
                descLabel.AddToClassList("reward-card-desc");
                body.Add(descLabel);

                if (captured.legendaryRelic != null && !string.IsNullOrEmpty(captured.targetUnitName))
                {
                    var forLabel = new Label($"\u2605 For: {captured.targetUnitName}");
                    forLabel.AddToClassList("reward-card-for");
                    body.Add(forLabel);
                }

                card.Add(body);

                string btnText = isMerchant ? $"Buy: {captured.goldCost}g" : "SELECT";
                bool canAfford = !isMerchant || currentGold < 0 || currentGold >= captured.goldCost;
                var selectBtn = new Button(() => onChoose?.Invoke(captured)) { text = btnText };
                selectBtn.AddToClassList("reward-button");
                selectBtn.SetEnabled(canAfford);
                if (!canAfford) card.style.opacity = new StyleFloat(0.45f);
                card.Add(selectBtn);

                _upgradeChoices.Add(card);
            }
        }

        // Leave button for merchant (always shown so the player can exit without buying)
        if (onSkip != null && _upgradeChoices != null)
        {
            var leaveBtn = new Button(() => onSkip.Invoke()) { text = "\u21A9  Leave" };
            leaveBtn.AddToClassList("merchant-leave-btn");
            _upgradeChoices.Add(leaveBtn);
        }

        if (_intermissionPanel != null)
            _intermissionPanel.style.display = DisplayStyle.Flex;
    }

    public void ShowRoomChoices(int clearedEncounter, List<RoomChoice> choices, Action<RoomChoice> onChoose)
    {
        if (!EnsureUiReady()) return;
        HideOverlays();
        ClearTargetButtons();

        if (_intermissionTitle != null) _intermissionTitle.text = "Choose Your Path";
        if (_intermissionGold != null) _intermissionGold.text = $"Encounter {clearedEncounter} cleared.";

        if (_upgradeChoices != null)
        {
            _upgradeChoices.Clear();
            _rerollContainer?.Clear();
            foreach (var choice in choices)
            {
                var captured = choice;
                var card = new VisualElement();
                card.AddToClassList("reward-card");

                var typeHeader = new VisualElement();
                typeHeader.AddToClassList("reward-card-type");
                var typeLabel = new Label(captured.encounterType.ToString());
                typeLabel.AddToClassList("reward-card-type-label");
                typeHeader.Add(typeLabel);
                card.Add(typeHeader);

                var body = new VisualElement();
                body.AddToClassList("reward-card-body");

                var titleLabel = new Label(captured.title);
                titleLabel.AddToClassList("reward-card-title");
                body.Add(titleLabel);

                var descLabel = new Label(captured.description);
                descLabel.AddToClassList("reward-card-desc");
                body.Add(descLabel);

                card.Add(body);

                var selectBtn = new Button(() => onChoose?.Invoke(captured)) { text = "ENTER" };
                selectBtn.AddToClassList("reward-button");
                card.Add(selectBtn);

                _upgradeChoices.Add(card);
            }
        }
        if (_intermissionPanel != null)
            _intermissionPanel.style.display = DisplayStyle.Flex;
    }

    public void ShowRunSummary(bool won, int reachedEncounter, int gold)
    {
        if (!EnsureUiReady()) return;
        HideOverlays();
        ClearTargetButtons();

        if (_summaryTitle != null)
            _summaryTitle.text = won ? "Run Complete!" : "Defeated!";

        if (_summaryStats != null)
        {
            string statsText = $"Reached Encounter {reachedEncounter}  •  ☴ {gold} Gold";
            var runStats = RunManager.Instance?.CurrentRunStats;
            if (runStats != null)
            {
                statsText += $"\n\n--- Combat Statistics ---\n" +
                             $"Damage Dealt: {runStats.totalDamageDealt}\n" +
                             $"Damage Taken: {runStats.totalDamageTaken}\n" +
                             $"Healing Done: {runStats.healingDone}\n" +
                             $"Critical Hits: {runStats.criticalHitsLanded}\n" +
                             $"Enemies Defeated: {runStats.enemiesDefeated}\n" +
                             $"Turns Survived: {runStats.turnsSurvived}";
            }
            _summaryStats.text = statsText;
        }

        if (_summaryPanel != null)
        {
            Button restartBtn = _summaryPanel.Q<Button>("summary-restart");
            if (restartBtn != null)
            {
                restartBtn.clicked -= OnRestartClicked;
                restartBtn.clicked += OnRestartClicked;
            }
            _summaryPanel.style.display = DisplayStyle.Flex;
        }
    }

    private void OnRestartClicked()
    {
        RunManager.Instance?.StartRun();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}