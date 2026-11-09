using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Hero select screen. Layout: Assets/UI Toolkit/MainMenu/MainMenu.uxml (+ MainMenuStyles.uss).
/// Stats and skills shown are read from the hero prefab's <see cref="Unit"/> component.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private string _battleSceneName = "BattleScene";
    [SerializeField] private UIDocument _uiDocument;

    private CharacterDefinitionSO[] _availableCharacters;
    private int _selectedIndex;

    private VisualElement _card;
    private VisualElement _detailsPanel;
    private VisualElement _characterPreviewImage;
    private Label _characterNameLabel;
    private Label _characterDescLabel;
    private VisualElement _statBars;
    private VisualElement _skillList;
    private VisualElement _heroList;
    private Label _detailsName;
    private bool _switching;

    // Bar scale: the value that fills a bar completely.
    private static readonly (string label, string icon, string fillClass, float max)[] StatDefs =
    {
        ("HP",  "icon-heart",  "stat-fill--hp", 150f),
        ("MP",  "icon-star",   "stat-fill--mp", 40f),
        ("ATK", "icon-sword",  null,            40f),
        ("DEF", "icon-shield", null,            25f),
        ("SPD", "icon-energy", null,            25f),
        ("LCK", "icon-clover", null,            80f),
    };

    private readonly List<VisualElement> _statFills = new List<VisualElement>();
    private readonly List<Label> _statValues = new List<Label>();

    private void Awake()
    {
        _availableCharacters = Resources.LoadAll<CharacterDefinitionSO>("Characters");
        if (_availableCharacters == null || _availableCharacters.Length == 0)
            Debug.LogError("[MainMenuManager] No CharacterDefinitionSO files found in Resources/Characters!");
    }

    private void Start()
    {
        if (_uiDocument == null) _uiDocument = GetComponent<UIDocument>();
        if (_uiDocument == null) return;

        var root = _uiDocument.rootVisualElement;

        _card = root.Q<VisualElement>("character-card");
        _detailsPanel = root.Q<VisualElement>("details-panel");
        _characterPreviewImage = root.Q<VisualElement>("character-preview");
        _characterNameLabel = root.Q<Label>("character-name");
        _characterDescLabel = root.Q<Label>("character-desc");
        _statBars = root.Q<VisualElement>("stat-bars");
        _skillList = root.Q<VisualElement>("skill-list");
        _heroList = root.Q<VisualElement>("hero-list");
        _detailsName = root.Q<Label>("details-name");

        var start = root.Q<Button>("start-btn");
        var quit = root.Q<Button>("quit-btn");
        if (start != null) start.clicked += OnStartRun;
        if (quit != null) quit.clicked += OnQuit;

        BuildStatBars();
        BuildHeroList();
        UpdateUI();
    }

    // ─── Navigation ───────────────────────────────────────────────────────

    private void Select(int index)
    {
        if (_availableCharacters == null || _availableCharacters.Length == 0 || _switching) return;
        if (index == _selectedIndex || index < 0 || index >= _availableCharacters.Length) return;
        _selectedIndex = index;

        // Fade the preview and panel out, swap the content, fade back in.
        _switching = true;
        _card?.AddToClassList("swapping");
        _detailsPanel?.AddToClassList("swapping");
        (_card ?? _detailsPanel)?.schedule.Execute(() =>
        {
            UpdateUI();
            _card?.RemoveFromClassList("swapping");
            _detailsPanel?.RemoveFromClassList("swapping");
            _switching = false;
        }).StartingIn(150);
    }

    // ─── Building blocks ──────────────────────────────────────────────────

    private void BuildStatBars()
    {
        if (_statBars == null) return;
        _statBars.Clear();
        _statFills.Clear();
        _statValues.Clear();

        foreach (var def in StatDefs)
        {
            var row = new VisualElement();
            row.AddToClassList("stat-row");

            var icon = new VisualElement();
            icon.AddToClassList("stat-icon");
            icon.AddToClassList(def.icon);
            row.Add(icon);

            var name = new Label(def.label);
            name.AddToClassList("stat-name");
            row.Add(name);

            var track = new VisualElement();
            track.AddToClassList("stat-track");
            var fill = new VisualElement();
            fill.AddToClassList("stat-fill");
            if (def.fillClass != null) fill.AddToClassList(def.fillClass);
            track.Add(fill);
            row.Add(track);

            var value = new Label("-");
            value.AddToClassList("stat-value");
            row.Add(value);

            _statBars.Add(row);
            _statFills.Add(fill);
            _statValues.Add(value);
        }
    }

    private void BuildHeroList()
    {
        if (_heroList == null) return;
        _heroList.Clear();
        if (_availableCharacters == null) return;

        for (int i = 0; i < _availableCharacters.Length; i++)
        {
            CharacterDefinitionSO def = _availableCharacters[i];
            int index = i;

            var entry = new VisualElement();
            entry.AddToClassList("hero-entry");
            entry.RegisterCallback<ClickEvent>(_ => Select(index));

            var icon = new VisualElement();
            icon.AddToClassList("hero-entry-icon");
            if (def.icon != null) icon.style.backgroundImage = new StyleBackground(def.icon);
            entry.Add(icon);

            var text = new VisualElement();
            var name = new Label(def.characterName);
            name.AddToClassList("hero-entry-name");
            text.Add(name);
            var sub = new Label(FirstSentence(def.description));
            sub.AddToClassList("hero-entry-sub");
            text.Add(sub);
            entry.Add(text);

            _heroList.Add(entry);
        }
    }

    private static string FirstSentence(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        int dot = text.IndexOfAny(new[] { '.', '!', '\n' });
        string first = dot > 0 ? text.Substring(0, dot) : text;
        return first.Length > 28 ? first.Substring(0, 26).TrimEnd() + "..." : first;
    }

    // ─── Content ──────────────────────────────────────────────────────────

    private void UpdateUI()
    {
        if (_availableCharacters == null || _availableCharacters.Length == 0) return;

        var selected = _availableCharacters[_selectedIndex];

        if (_characterNameLabel != null) _characterNameLabel.text = selected.characterName;
        if (_characterDescLabel != null) _characterDescLabel.text = selected.description;
        if (_characterPreviewImage != null && selected.icon != null)
            _characterPreviewImage.style.backgroundImage = new StyleBackground(selected.icon);

        Unit unit = selected.characterPrefab != null ? selected.characterPrefab.GetComponent<Unit>() : null;
        UpdateStats(unit);
        UpdateSkills(unit);

        if (_detailsName != null) _detailsName.text = selected.characterName;

        if (_heroList != null)
            for (int i = 0; i < _heroList.childCount; i++)
                _heroList[i].EnableInClassList("hero-entry--selected", i == _selectedIndex);
    }

    private void UpdateStats(Unit unit)
    {
        if (_statFills.Count != StatDefs.Length) return;

        float[] values = unit == null
            ? new float[StatDefs.Length]
            : new float[] { unit.maxHealth, unit.maxMana, unit.attack, unit.defense, unit.speed, unit.luck };

        for (int i = 0; i < StatDefs.Length; i++)
        {
            _statValues[i].text = unit == null ? "-" : Mathf.RoundToInt(values[i]).ToString();
            _statFills[i].style.width = Length.Percent(Mathf.Clamp01(values[i] / StatDefs[i].max) * 100f);
        }
    }

    private void UpdateSkills(Unit unit)
    {
        if (_skillList == null) return;
        _skillList.Clear();
        if (unit == null) return;

        if (unit.basicAttack != null) AddSkill(unit.basicAttack, "Basic attack");
        if (unit.specialAttacks != null)
            foreach (AttackAction special in unit.specialAttacks)
                if (special != null) AddSkill(special, "Special");

        if (unit.unlockableAttacks != null && unit.unlockableAttacks.Count > 0)
        {
            var names = new List<string>();
            foreach (AttackAction a in unit.unlockableAttacks)
                if (a != null) names.Add(a.actionName);
            if (names.Count > 0)
            {
                var note = new Label("Can learn during a run: " + string.Join(", ", names));
                note.AddToClassList("skill-note");
                _skillList.Add(note);
            }
        }
    }

    private void AddSkill(AttackAction attack, string kind)
    {
        var box = new VisualElement();
        box.AddToClassList("skill");

        string cost = attack.manaCost > 0 ? $"  ({attack.manaCost} MP)" : "";
        var name = new Label($"{attack.actionName}{cost}");
        name.AddToClassList("skill-name");
        box.Add(name);

        // Compact one-line summary of the same text the battle tooltips use.
        string summary = BattleUIManager.DescribeAttack(attack).Replace("\n", "  ");
        var desc = new Label($"{kind}: {summary}");
        desc.AddToClassList("skill-desc");
        box.Add(desc);

        _skillList.Add(box);
    }

    // ─── Buttons ──────────────────────────────────────────────────────────

    private void OnStartRun()
    {
        if (_availableCharacters == null || _availableCharacters.Length == 0)
        {
            Debug.LogWarning("No character selected!");
            return;
        }

        var selected = _availableCharacters[_selectedIndex];

        if (RunManager.Instance == null)
        {
            GameObject rmObj = new GameObject("RunManager");
            rmObj.AddComponent<RunManager>();
        }

        RunManager.Instance.SelectedCharacterPrefab = selected.characterPrefab;
        RunManager.Instance.StartRun();
        SceneFader.Load(_battleSceneName);
    }

    private void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
