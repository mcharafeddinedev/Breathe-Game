using UnityEngine;
using Breathe.Data;
using Breathe.Utility;

namespace Breathe.Input
{
    /// <summary>
    /// Captures breath input from the system's default microphone.
    /// Computes RMS amplitude each frame, applies low-frequency weighting
    /// to favour breath over speech, smooths and maps through a calibrated
    /// range to produce a 0-1 intensity signal.
    /// </summary>
    public sealed class MicBreathInput : MonoBehaviour, IBreathInput
    {
        [Header("Configuration")]
        [SerializeField, Tooltip("Shared breath-processing parameters.")]
        private BreathConfig breathConfig;

        [Header("Microphone Settings")]
        [SerializeField, Tooltip("Recording sample rate in Hz.")]
        private int sampleRate = 44100;

        [SerializeField, Tooltip("Number of audio samples analysed per frame for RMS.")]
        private int sampleWindow = 1024;

        [Header("Low-Frequency Bias")]
        [SerializeField, Tooltip("Number of low-frequency FFT bins used as the 'breath band'. " +
            "Lower bins correspond to lower frequencies.")]
        private int lowFreqBinCount = 8;

        [SerializeField, Tooltip("Total FFT bins used for spectrum analysis. Must be a power of two.")]
        private int spectrumSize = 64;

        [SerializeField, Tooltip("Weight applied to the low-frequency energy ratio. " +
            "1.0 = no bias, higher values increase breath-band preference.")]
        private float lowFreqWeight = 2f;

        private AudioClip _micClip;
        private string _deviceName;
        private float _smoothedAmplitude;
        private float _smoothedIntensity;
        private int _previousLevel;
        private bool _active;

        private float[] _sampleBuffer;
        private float[] _spectrumBuffer;

        // Calibration overrides (runtime); fall back to BreathConfig values.
        private float _calibrationBaseline = -1f;
        private float _calibrationMax = -1f;

        // ------------------------------------------------------------------ IBreathInput
        /// <summary>
        /// Returns the calibrated, smoothed breath intensity in [0, 1].
        /// </summary>
        public float GetBreathIntensity() => _smoothedIntensity;

        /// <summary>
        /// Returns a discrete power level in [0, 5] based on configured thresholds.
        /// </summary>
        public int GetBreathLevel()
        {
            return SignalProcessing.GetPowerLevel(_smoothedIntensity, breathConfig.PowerLevelThresholds);
        }

        /// <summary>
        /// True when the intensity exceeds the dead-zone threshold.
        /// </summary>
        public bool IsBreathing() => _smoothedIntensity > breathConfig.DeadZoneThreshold;

        /// <summary>
        /// Starts recording from the default microphone.
        /// </summary>
        public void Initialize()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[BreathInput] No microphone devices found.");
                return;
            }

            _deviceName = Microphone.devices[0];
            _micClip = Microphone.Start(null, true, 1, sampleRate);
            _sampleBuffer = new float[sampleWindow];
            _spectrumBuffer = new float[spectrumSize];
            _smoothedAmplitude = 0f;
            _smoothedIntensity = 0f;
            _previousLevel = 0;
            _calibrationBaseline = -1f;
            _calibrationMax = -1f;
            _active = true;
            enabled = true;

            Debug.Log("[BreathInput] Mic initialized: " + _deviceName);
        }

        /// <summary>
        /// Stops the microphone and releases resources.
        /// </summary>
        public void Shutdown()
        {
            _active = false;

            if (Microphone.IsRecording(null))
            {
                Microphone.End(null);
            }

            _micClip = null;
            _smoothedAmplitude = 0f;
            _smoothedIntensity = 0f;
            enabled = false;
        }

        // ------------------------------------------------------------------ Calibration

        /// <summary>
        /// Records the current smoothed amplitude as the silence / baseline floor.
        /// </summary>
        public void SetBaseline()
        {
            _calibrationBaseline = _smoothedAmplitude;
            Debug.Log($"[BreathInput] Mic calibration baseline set to {_calibrationBaseline:F4}");
        }

        /// <summary>
        /// Records the current smoothed amplitude as the maximum expected breath.
        /// </summary>
        public void SetMax()
        {
            _calibrationMax = _smoothedAmplitude;
            Debug.Log($"[BreathInput] Mic calibration max set to {_calibrationMax:F4}");
        }

        // ------------------------------------------------------------------ Unity lifecycle
        private void Update()
        {
            if (!_active || _micClip == null || breathConfig == null) return;

            float rawRms = ComputeRms();
            float lowFreqBias = ComputeLowFrequencyBias();

            float biasedRms = rawRms * lowFreqBias;

            _smoothedAmplitude = SignalProcessing.ExponentialMovingAverage(
                _smoothedAmplitude, biasedRms, breathConfig.SmoothingFactor);

            float baseline = _calibrationBaseline >= 0f
                ? _calibrationBaseline
                : breathConfig.CalibrationBaseline;

            float max = _calibrationMax >= 0f
                ? _calibrationMax
                : breathConfig.CalibrationMax;

            _smoothedIntensity = SignalProcessing.MapRange(
                _smoothedAmplitude, baseline, max, 0f, 1f);

            _smoothedIntensity = SignalProcessing.DeadZone(
                _smoothedIntensity, breathConfig.DeadZoneThreshold);

            _smoothedIntensity = Mathf.Clamp01(_smoothedIntensity);

            CheckLevelCrossing();
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        // ------------------------------------------------------------------ Internal
        private float ComputeRms()
        {
            int micPosition = Microphone.GetPosition(null);
            if (micPosition < sampleWindow) return 0f;

            _micClip.GetData(_sampleBuffer, micPosition - sampleWindow);

            float sum = 0f;
            for (int i = 0; i < _sampleBuffer.Length; i++)
            {
                sum += _sampleBuffer[i] * _sampleBuffer[i];
            }

            return Mathf.Sqrt(sum / _sampleBuffer.Length);
        }

        /// <summary>
        /// Computes a multiplier in [1, lowFreqWeight] that increases when
        /// most audio energy sits in the low-frequency bins (breath-like).
        /// Returns 1 when energy is evenly distributed or dominated by high freqs.
        /// </summary>
        private float ComputeLowFrequencyBias()
        {
            AudioListener.GetSpectrumData(_spectrumBuffer, 0, FFTWindow.BlackmanHarris);

            float lowEnergy = 0f;
            float totalEnergy = 0f;

            for (int i = 0; i < _spectrumBuffer.Length; i++)
            {
                float e = _spectrumBuffer[i] * _spectrumBuffer[i];
                totalEnergy += e;
                if (i < lowFreqBinCount)
                {
                    lowEnergy += e;
                }
            }

            if (totalEnergy <= 0f) return 1f;

            float ratio = lowEnergy / totalEnergy;
            return Mathf.Lerp(1f, lowFreqWeight, ratio);
        }

        private void CheckLevelCrossing()
        {
            int level = GetBreathLevel();
            if (level != _previousLevel)
            {
                Debug.Log($"[BreathInput] Mic intensity level changed to {level}");
                _previousLevel = level;
            }
        }
    }
}
