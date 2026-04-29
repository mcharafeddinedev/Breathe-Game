using System.Collections.Generic;
using UnityEngine;
using Breathe.Audio;
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
        private float _nextPrimarySfxUnscaledTime;

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

        protected virtual void Start()
        {
            // Awake ordering: if Sailboat ran before MinigameManager, registration was skipped above.
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

            var sfx = Definition?.MinigameSfxProfile;
            if (SfxPlayer.Instance != null && sfx != null)
            {
                SfxPlayer.Instance.PlayMinigameGameplayStart(sfx);
                SfxPlayer.Instance.SetAmbienceLoop(sfx);
            }
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
            SfxPlayer.Instance?.StopAmbienceLoop();
            if (_breathAnalytics != null) _breathAnalytics.StopTracking();
            LogSession();
        }

        /// <summary>Uses MinigameSfxProfile.PrimaryAction (e.g. chiptune hit). Optional cooldown avoids spam (e.g. bubbles).</summary>
        /// <param name="volumeScale">Multiplies baked-in level (0.88).</param>
        protected void TryPlayMinigamePrimaryActionSfx(float minSecondsBetween = 0f, float volumeScale = 1f)
        {
            var clip = Definition?.MinigameSfxProfile?.PrimaryAction;
            if (clip == null || SfxPlayer.Instance == null) return;
            if (minSecondsBetween > 0f && Time.unscaledTime < _nextPrimarySfxUnscaledTime) return;
            if (minSecondsBetween > 0f)
                _nextPrimarySfxUnscaledTime = Time.unscaledTime + minSecondsBetween;
            SfxPlayer.Instance.PlayClip(clip, 0.88f * Mathf.Clamp01(volumeScale));
        }

        /// <summary>Uses MinigameSfxProfile.SpecialEvent (e.g. constellation reveal).</summary>
        protected void PlayMinigameSpecialEventSfx()
        {
            var clip = Definition?.MinigameSfxProfile?.SpecialEvent;
            if (clip != null && SfxPlayer.Instance != null)
                SfxPlayer.Instance.PlayClip(clip, 0.92f);
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
