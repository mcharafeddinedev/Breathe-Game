using System.Collections.Generic;
using UnityEngine;
using Breathe.Data;

namespace Breathe.Gameplay
{
    // Shared boilerplate for all minigame controllers.
    // Handles MinigameManager registration/cleanup, BreathAnalytics lifecycle,
    // session logging, and MinigameDefinition resolution.
    // Each concrete minigame inherits this and overrides the abstract/virtual members.
    public abstract class MinigameBase : MonoBehaviour, IMinigame
    {
        [Header("Shared References")]
        [SerializeField] protected BreathAnalytics _breathAnalytics;

        private MinigameDefinition _cachedDefinition;

        public abstract string MinigameId { get; }
        public abstract bool IsComplete { get; }

        // Lazy-resolved from MinigameManager's roster by MinigameId.
        public MinigameDefinition Definition
        {
            get
            {
                if (_cachedDefinition == null && MinigameManager.Instance != null)
                    _cachedDefinition = MinigameManager.Instance.GetDefinitionById(MinigameId);
                return _cachedDefinition;
            }
        }

        protected virtual void Awake()
        {
            if (MinigameManager.Instance != null)
                MinigameManager.Instance.RegisterActiveMinigame(this);
        }

        protected virtual void OnDestroy()
        {
            if (MinigameManager.Instance != null &&
                ReferenceEquals(MinigameManager.Instance.ActiveMinigame, this))
                MinigameManager.Instance.ClearActiveMinigame();
        }

        public virtual void OnMinigameStart()
        {
            if (_breathAnalytics != null) _breathAnalytics.ResetAll();
            ApplySpinDownConfig();
        }

        private void ApplySpinDownConfig()
        {
            if (BreathPowerSystem.Instance == null) return;
            var def = Definition;
            if (def != null)
                BreathPowerSystem.Instance.ConfigureSpinDown(
                    def.SpinDownThreshold, def.SpinDownWindow, def.SpinDownResumeDelta);
            else
                BreathPowerSystem.Instance.ResetSpinDownDefaults();
        }

        public virtual void OnMinigameEnd()
        {
            if (_breathAnalytics != null) _breathAnalytics.StopTracking();
            LogSession();
        }

        public abstract MinigameStat[] GetEndStats();
        public virtual string GetCelebrationTitle() => "WELL  DONE!";
        public virtual string GetPersonalBestMessage() => "";
        public virtual string GetResultTitle() => "COMPLETE";
        public virtual Dictionary<string, string> GetDebugInfo() => null;

        protected void LogSession()
        {
            if (SessionLogger.Instance == null) return;
            SessionLogger.Instance.LogRound(MinigameId, GetEndStats(), _breathAnalytics);
        }
    }
}
