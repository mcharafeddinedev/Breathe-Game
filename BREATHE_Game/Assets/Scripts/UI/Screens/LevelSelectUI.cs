using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Breathe.Data;
using Breathe.Input;
using Breathe.Gameplay;

namespace Breathe.UI
{
    // Minigame grid (2x3). Each cell shows name, breath-pattern tag, and thumbnail.
    // Locked minigames are greyed out. Selecting one transitions to Calibration or Playing.
    public sealed class LevelSelectUI : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField] private Transform _gridParent;

        [SerializeField, Tooltip("Prefab for a single minigame card (Button + Image + TMP labels).")]
        private GameObject _cardPrefab;

        [SerializeField] private Button _backButton;

        [Header("Info Panel")]
        [SerializeField] private TextMeshProUGUI _selectedNameText;
        [SerializeField] private TextMeshProUGUI _selectedDescriptionText;

        [SerializeField, Tooltip("Breath pattern label for the selected minigame.")]
        private TextMeshProUGUI _selectedPatternText;

        private MinigameManager _minigameManager;

        private void Start()
        {
            _minigameManager = MinigameManager.Instance;

            if (_backButton != null)
                _backButton.onClick.AddListener(OnBack);

            PopulateGrid();
        }

        private void OnDestroy()
        {
            if (_backButton != null)
                _backButton.onClick.RemoveListener(OnBack);
        }

        // Builds one card per MinigameDefinition. Falls back to binding existing
        // child buttons if no prefab is assigned.
        private void PopulateGrid()
        {
            if (_minigameManager == null)
            {
                Debug.LogWarning("[LevelSelectUI] MinigameManager not found. Grid will be empty.");
                return;
            }

            MinigameDefinition[] defs = _minigameManager.AvailableMinigames;
            if (defs == null || defs.Length == 0) return;

            if (_cardPrefab != null && _gridParent != null)
                BuildCardsFromPrefab(defs);
            else if (_gridParent != null)
                BindExistingChildren(defs);
        }

        private void BuildCardsFromPrefab(MinigameDefinition[] defs)
        {
            foreach (var def in defs)
            {
                if (def == null) continue;

                GameObject card = Instantiate(_cardPrefab, _gridParent);
                card.name = $"Card_{def.MinigameId}";

                var nameLabel = card.GetComponentInChildren<TextMeshProUGUI>();
                if (nameLabel != null)
                    nameLabel.text = def.DisplayName;

                var thumbnail = card.transform.Find("Thumbnail")?.GetComponent<Image>();
                if (thumbnail != null && def.Thumbnail != null)
                    thumbnail.sprite = def.Thumbnail;

                var button = card.GetComponent<Button>();
                if (button != null)
                {
                    button.interactable = def.IsUnlocked;
                    var captured = def;
                    button.onClick.AddListener(() => OnSelectMinigame(captured));
                }
            }
        }

        // Binds pre-placed editor children to definitions by index.
        private void BindExistingChildren(MinigameDefinition[] defs)
        {
            int count = Mathf.Min(defs.Length, _gridParent.childCount);
            for (int i = 0; i < count; i++)
            {
                var child = _gridParent.GetChild(i);
                var def = defs[i];
                if (def == null) continue;

                var nameLabel = child.GetComponentInChildren<TextMeshProUGUI>();
                if (nameLabel != null)
                    nameLabel.text = def.DisplayName;

                var button = child.GetComponent<Button>();
                if (button != null)
                {
                    button.interactable = def.IsUnlocked;
                    var captured = def;
                    button.onClick.AddListener(() => OnSelectMinigame(captured));
                }
            }
        }

        private void OnSelectMinigame(MinigameDefinition def)
        {
            if (def == null || !def.IsUnlocked) return;

            _minigameManager.SelectMinigame(def);

            if (_selectedNameText != null)
                _selectedNameText.text = def.DisplayName;
            if (_selectedDescriptionText != null)
                _selectedDescriptionText.text = def.Description;
            if (_selectedPatternText != null)
                _selectedPatternText.text = def.BreathPattern;

            GameStateManager gsm = GameStateManager.Instance;
            if (gsm == null) return;

            bool requiresCalibration = false;
            if (BreathInputManager.Instance != null)
            {
                var mode = BreathInputManager.Instance.CurrentMode;
                requiresCalibration = mode == InputMode.Microphone || mode == InputMode.Fan;
            }

            gsm.TransitionTo(requiresCalibration ? GameState.Calibration : GameState.Playing);
        }

        private void OnBack()
        {
            GameStateManager gsm = GameStateManager.Instance;
            if (gsm != null)
                gsm.TransitionTo(GameState.MainMenu);
        }
    }
}
