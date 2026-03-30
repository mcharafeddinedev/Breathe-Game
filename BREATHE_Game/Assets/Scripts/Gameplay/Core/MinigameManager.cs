using System;
using UnityEngine;
using Breathe.Data;

namespace Breathe.Gameplay
{
    // Singleton that tracks which minigame is selected and which IMinigame
    // implementation is currently active. Bridges level-select UI to gameplay.
    public class MinigameManager : MonoBehaviour
    {
        private static MinigameManager _instance;
        public static MinigameManager Instance => _instance;

        [Header("Minigame Roster")]
        [SerializeField, Tooltip("All minigames in level select. Order matches the grid.")]
        private MinigameDefinition[] _availableMinigames = Array.Empty<MinigameDefinition>();

        private MinigameDefinition _selectedDefinition;
        private IMinigame _activeMinigame;

        public MinigameDefinition SelectedDefinition => _selectedDefinition;
        public IMinigame ActiveMinigame => _activeMinigame;
        public MinigameDefinition[] AvailableMinigames => _availableMinigames;

        public event Action<MinigameDefinition> OnMinigameSelected;
        public event Action<IMinigame> OnActiveMinigameChanged;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            if (_availableMinigames.Length > 0 && _selectedDefinition == null)
                _selectedDefinition = _availableMinigames[0];
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // Called by level select UI when a minigame is picked
        public void SelectMinigame(MinigameDefinition definition)
        {
            if (definition == null) return;
            _selectedDefinition = definition;
            Debug.Log($"[MinigameManager] Selected: {definition.DisplayName}");
            OnMinigameSelected?.Invoke(definition);
        }

        // Called by a minigame controller on init to register itself
        public void RegisterActiveMinigame(IMinigame minigame)
        {
            _activeMinigame = minigame;
            Debug.Log($"[MinigameManager] Active: {minigame?.MinigameId ?? "null"}");
            OnActiveMinigameChanged?.Invoke(_activeMinigame);
        }

        public void ClearActiveMinigame()
        {
            _activeMinigame = null;
            OnActiveMinigameChanged?.Invoke(null);
        }

        public MinigameDefinition GetDefinitionById(string minigameId)
        {
            foreach (var def in _availableMinigames)
                if (def != null && def.MinigameId == minigameId) return def;
            return null;
        }
    }
}
