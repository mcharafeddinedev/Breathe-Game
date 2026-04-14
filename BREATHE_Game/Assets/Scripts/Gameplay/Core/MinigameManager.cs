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
        [SerializeField, Tooltip("Full minigame roster (includes hidden-from-menu entries for lookups and future content).")]
        private MinigameDefinition[] _availableMinigames = Array.Empty<MinigameDefinition>();

        private MinigameDefinition _selectedDefinition;
        private IMinigame _activeMinigame;
        private MinigameDefinition[] _minigamesForLevelSelect;

        public MinigameDefinition SelectedDefinition => _selectedDefinition;
        public IMinigame ActiveMinigame => _activeMinigame;
        /// <summary>Full roster (editor tests, tutorials matching by scene, GetDefinitionById).</summary>
        public MinigameDefinition[] AvailableMinigames => _availableMinigames;
        /// <summary>Subset shown on level select (IncludeInLevelSelect).</summary>
        public MinigameDefinition[] MinigamesForLevelSelect => _minigamesForLevelSelect;

        public event Action<MinigameDefinition> OnMinigameSelected;
        public event Action<IMinigame> OnActiveMinigameChanged;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            RebuildLevelSelectCache();
            if (_selectedDefinition == null)
                _selectedDefinition = GetDefaultSelection();
        }

        private void RebuildLevelSelectCache()
        {
            int count = 0;
            for (int i = 0; i < _availableMinigames.Length; i++)
            {
                var d = _availableMinigames[i];
                if (d != null && d.IncludeInLevelSelect) count++;
            }

            _minigamesForLevelSelect = new MinigameDefinition[count];
            int w = 0;
            for (int i = 0; i < _availableMinigames.Length; i++)
            {
                var d = _availableMinigames[i];
                if (d != null && d.IncludeInLevelSelect)
                    _minigamesForLevelSelect[w++] = d;
            }
        }

        private MinigameDefinition GetDefaultSelection()
        {
            if (_minigamesForLevelSelect != null && _minigamesForLevelSelect.Length > 0)
                return _minigamesForLevelSelect[0];
            foreach (var d in _availableMinigames)
                if (d != null) return d;
            return null;
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
