using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private string _battleSceneName = "BattleScene";

    // UI Toolkit Document
    [SerializeField] private UIDocument _uiDocument;

    // Character Data path
    private CharacterDefinitionSO[] _availableCharacters;
    private int _selectedIndex = 0;

    // UI Elements
    private VisualElement _characterPreviewImage;
    private Label _characterNameLabel;
    private Label _characterDescLabel;
    private Button _prevBtn;
    private Button _nextBtn;
    private Button _startBtn;

    private void Awake()
    {
        // Load all character definitions from Resources
        _availableCharacters = Resources.LoadAll<CharacterDefinitionSO>("Characters");
        if (_availableCharacters == null || _availableCharacters.Length == 0)
        {
            Debug.LogError("[MainMenuManager] No CharacterDefinitionSO files found in Resources/Characters!");
        }
    }

    private void Start()
    {
        if (_uiDocument == null) _uiDocument = GetComponent<UIDocument>();
        if (_uiDocument == null) return;

        var root = _uiDocument.rootVisualElement;

        // Bind Elements (Requires UXML setup with these names)
        _characterPreviewImage = root.Q<VisualElement>("character-preview");
        _characterNameLabel = root.Q<Label>("character-name");
        _characterDescLabel = root.Q<Label>("character-desc");

        _prevBtn = root.Q<Button>("prev-btn");
        _nextBtn = root.Q<Button>("next-btn");
        _startBtn = root.Q<Button>("start-btn");

        if (_prevBtn != null) _prevBtn.clicked += OnPrevSelected;
        if (_nextBtn != null) _nextBtn.clicked += OnNextSelected;
        if (_startBtn != null) _startBtn.clicked += OnStartRun;

        UpdateUI();
    }

    private void OnPrevSelected()
    {
        if (_availableCharacters == null || _availableCharacters.Length == 0) return;
        _selectedIndex--;
        if (_selectedIndex < 0) _selectedIndex = _availableCharacters.Length - 1;
        UpdateUI();
    }

    private void OnNextSelected()
    {
        if (_availableCharacters == null || _availableCharacters.Length == 0) return;
        _selectedIndex++;
        if (_selectedIndex >= _availableCharacters.Length) _selectedIndex = 0;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_availableCharacters == null || _availableCharacters.Length == 0) return;

        var selected = _availableCharacters[_selectedIndex];

        if (_characterNameLabel != null) _characterNameLabel.text = selected.characterName;
        if (_characterDescLabel != null) _characterDescLabel.text = selected.description;

        if (_characterPreviewImage != null && selected.icon != null)
        {
            _characterPreviewImage.style.backgroundImage = new StyleBackground(selected.icon);
        }
    }

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
        SceneManager.LoadScene(_battleSceneName);
    }
}
