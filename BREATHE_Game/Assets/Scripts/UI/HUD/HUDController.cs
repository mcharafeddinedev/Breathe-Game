using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Breathe.Gameplay;
using Breathe.Utility;

namespace Breathe.UI
{
    // In-race HUD: wind power meter, race progress bar, popup text, and countdown.
    public sealed class HUDController : MonoBehaviour
    {
        [Header("Wind Meter")]
        [SerializeField, Tooltip("fillAmount reflects current wind power (0–1).")]
        private Image windMeterFill;

        [Header("Race Progress")]
        [SerializeField] private RectTransform progressBarRect;

        [SerializeField, Tooltip("Index 0 = player, 1–2 = AI.")]
        private RectTransform[] boatIcons = new RectTransform[3];

        [Header("Pop-up Text")]
        [SerializeField] private TextMeshProUGUI popupText;

        [SerializeField, Tooltip("Pixels below top of screen. 80–120 keeps it top-center, not flush with edge.")]
        private float popupTopOffset = 100f;

        [SerializeField, Tooltip("Duration of the bouncy scale-up phase.")]
        private float popupPopInDuration = 0.35f;

        [SerializeField] private float popupHoldDuration = 1.2f;
        [SerializeField] private float popupPopOutDuration = 0.25f;

        [SerializeField, Tooltip("Bounce intensity on pop-in. Higher = more overshoot.")]
        private float popupOvershoot = 1.70158f;

        [Header("Countdown")]
        [SerializeField, Tooltip("Large center-screen text for the 3-2-1-GO countdown.")]
        private TextMeshProUGUI countdownText;

        [SerializeField] private float countdownScale = 1.5f;

        private BreathPowerSystem _breathPowerSystem;
        private RaceProgressTracker _raceProgressTracker;
        private Coroutine _activePopup;
        private Coroutine _activeCountdown;

        private void Start()
        {
            if (_breathPowerSystem == null)
                _breathPowerSystem = FindAnyObjectByType<BreathPowerSystem>();

            if (_raceProgressTracker == null)
                _raceProgressTracker = FindAnyObjectByType<RaceProgressTracker>();

            if (popupText != null)
                popupText.gameObject.SetActive(false);

            if (countdownText != null)
                countdownText.gameObject.SetActive(false);

            ZoneEvents.OnZonePopup += HandleZonePopup;
            Debug.Log("[HUDController] Subscribed to ZoneEvents.OnZonePopup");

            if (GameStateManager.Instance != null)
                GameStateManager.Instance.OnCountdownTick += HandleCountdownTick;
        }

        private void OnDestroy()
        {
            ZoneEvents.OnZonePopup -= HandleZonePopup;
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.OnCountdownTick -= HandleCountdownTick;
        }

        private void HandleZonePopup(string zoneText)
        {
            Debug.Log($"[HUDController.HandleZonePopup] \"{zoneText}\" popupText={popupText != null}");
            if (!string.IsNullOrEmpty(zoneText))
                ShowPopup(zoneText);
        }

        private void Update()
        {
            UpdateWindMeter();
            UpdateBoatPositions();
        }

        private void UpdateWindMeter()
        {
            if (windMeterFill == null || _breathPowerSystem == null) return;
            windMeterFill.fillAmount = Mathf.Clamp01(_breathPowerSystem.BreathPower);
        }

        private void UpdateBoatPositions()
        {
            if (_raceProgressTracker == null || progressBarRect == null) return;

            float barWidth = progressBarRect.rect.width;
            int boatCount = Mathf.Min(boatIcons.Length, _raceProgressTracker.GetBoatCount());

            for (int i = 0; i < boatCount; i++)
            {
                if (boatIcons[i] == null) continue;

                float progress = Mathf.Clamp01(_raceProgressTracker.GetProgress(i));
                float xPos = Mathf.Lerp(0f, barWidth, progress);

                Vector2 anchored = boatIcons[i].anchoredPosition;
                anchored.x = xPos;
                boatIcons[i].anchoredPosition = anchored;
            }
        }

        // Bounces in from zero scale, holds briefly, then shrinks out.
        public void ShowPopup(string text)
        {
            if (popupText == null) return;

            if (_activePopup != null)
                StopCoroutine(_activePopup);

            _activePopup = StartCoroutine(PopupRoutine(text));
        }

        private IEnumerator PopupRoutine(string text)
        {
            popupText.text = text;
            popupText.color = new Color(popupText.color.r, popupText.color.g, popupText.color.b, 1f);
            popupText.overflowMode = TMPro.TextOverflowModes.Overflow;
            popupText.textWrappingMode = TMPro.TextWrappingModes.Normal;

            RectTransform rt = popupText.rectTransform;
            if (rt != null)
            {
                Vector2 size = rt.sizeDelta;
                rt.sizeDelta = new Vector2(Mathf.Max(size.x, 520f), Mathf.Max(size.y, 85f));

                // Top-center: anchor to top, center horizontally; offset down from top edge
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -popupTopOffset);
            }
            popupText.gameObject.SetActive(true);

            yield return UIPopAnimation.PopInHoldOut(
                popupText.transform,
                popupPopInDuration,
                popupHoldDuration,
                popupPopOutDuration,
                Vector3.one,
                popupOvershoot,
                deactivateOnComplete: true);

            _activePopup = null;
        }

        private void HandleCountdownTick(int remaining)
        {
            if (countdownText == null) return;

            if (_activeCountdown != null)
                StopCoroutine(_activeCountdown);

            _activeCountdown = StartCoroutine(CountdownTickRoutine(remaining));
        }

        private IEnumerator CountdownTickRoutine(int remaining)
        {
            string label = remaining > 0 ? remaining.ToString() : "GO!";
            countdownText.text = label;
            countdownText.gameObject.SetActive(true);

            Vector3 targetScale = Vector3.one * countdownScale;

            yield return UIPopAnimation.PopInHoldOut(
                countdownText.transform,
                popInDuration: 0.3f,
                holdDuration: 0.4f,
                popOutDuration: 0.2f,
                targetScale: targetScale,
                overshoot: popupOvershoot * 1.2f,
                deactivateOnComplete: true);

            _activeCountdown = null;
        }
    }
}
