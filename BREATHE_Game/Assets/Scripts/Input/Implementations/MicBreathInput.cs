using UnityEngine;
using Breathe.Data;
using Breathe.Utility;

namespace Breathe.Input
{
    // Reads breath from the default microphone. Computes RMS amplitude,
    // applies a low-frequency bias (breath is mostly low freq, unlike speech),
    // then smooths + maps through a calibrated range to get a 0-1 signal.
    //
    // Platform support:
    // - Desktop/Mobile: Uses Unity's Microphone class
    // - WebGL: Uses WebGLMicrophoneBridge (Web Audio API via jslib plugin)
    public sealed class MicBreathInput : MonoBehaviour, IBreathInput
    {
        [SerializeField] private BreathConfig breathConfig;

        [Header("Microphone")]
#if !UNITY_WEBGL || UNITY_EDITOR
        [SerializeField] private int sampleRate = 44100;
        [SerializeField] private int sampleWindow = 1024;
#endif

        [Header("Low-Frequency Bias")]
        [SerializeField, Tooltip("How many low FFT bins count as 'breath band'.")]
        private int lowFreqBinCount = 8;
#if !UNITY_WEBGL || UNITY_EDITOR
        [SerializeField] private int spectrumSize = 64; // must be power of two
#endif
        [SerializeField, Tooltip("1.0 = no bias, higher = prefer low freqs more.")]
        private float lowFreqWeight = 2f;

        private AudioClip _micClip;
#if !UNITY_WEBGL || UNITY_EDITOR
        private string _deviceName;
#endif
        private float _smoothedAmplitude;
        private float _smoothedIntensity;
        private int _previousLevel;
        private bool _active;
        private float[] _sampleBuffer;
        private float[] _spectrumBuffer;
#if UNITY_WEBGL && !UNITY_EDITOR
        private bool _useWebGLBridge;
        private bool _webGLPermissionPending;
#endif

        // Runtime calibration — negative means "not set, use BreathConfig defaults"
        private float _calibrationBaseline = -1f;
        private float _calibrationMax = -1f;

        private float _diagTimer;
        private const float DiagIntervalSec = 2f;

        public static bool IsWebGLMicSupported
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return WebGLMicrophoneBridge.IsSupported;
#else
                return false;
#endif
            }
        }

        public float GetBreathIntensity() => _smoothedIntensity;

        public int GetBreathLevel()
        {
            return SignalProcessing.GetPowerLevel(_smoothedIntensity, breathConfig.PowerLevelThresholds);
        }

        public bool IsBreathing() => _smoothedIntensity > breathConfig.DeadZoneThreshold;

        public void Initialize()
        {
            _smoothedAmplitude = 0f;
            _smoothedIntensity = 0f;
            _previousLevel = 0;
            _calibrationBaseline = -1f;
            _calibrationMax = -1f;

#if UNITY_WEBGL && !UNITY_EDITOR
            _useWebGLBridge = false;
            _webGLPermissionPending = false;
            // WebGL: Use Web Audio API bridge instead of Unity's Microphone class
            if (!WebGLMicrophoneBridge.IsSupported)
            {
                Debug.LogError("[BreathInput] Microphone input FAILED — this browser does not support getUserMedia. " +
                    "Try a modern browser like Chrome, Firefox, or Edge.");
                _active = false;
                return;
            }

            _useWebGLBridge = true;
            _webGLPermissionPending = true;

            if (!WebGLMicrophoneBridge.StartMicrophone())
            {
                Debug.LogError("[BreathInput] Microphone input FAILED — could not request microphone access.");
                _active = false;
                return;
            }

            // Note: Permission is async in browsers. We'll check status in Update().
            _active = true;
            enabled = true;
            Debug.Log("[BreathInput] Microphone input INITIALIZING — waiting for browser permission...");
#else
            // Desktop/Mobile: Use Unity's native Microphone API
            if (Microphone.devices.Length == 0)
            {
                Debug.LogError("[BreathInput] Microphone input FAILED — no microphone detected on this system. " +
                    "Please connect a microphone and restart.");
                _active = false;
                return;
            }

            _deviceName = Microphone.devices[0];
            _micClip = Microphone.Start(null, true, 1, sampleRate);

            if (_micClip == null)
            {
                Debug.LogError($"[BreathInput] Microphone input FAILED — could not start recording on default device \"{_deviceName}\". " +
                    "The device may be in use by another application.");
                _active = false;
                return;
            }

            _sampleBuffer = new float[sampleWindow];
            _spectrumBuffer = new float[spectrumSize];
            _active = true;
            enabled = true;

            int deviceCount = Microphone.devices.Length;
            Debug.Log($"[BreathInput] Microphone input ENABLED — using default device: \"{_deviceName}\" @ {sampleRate}Hz " +
                $"({deviceCount} device{(deviceCount > 1 ? "s" : "")} available)");
#endif
        }

        public void Shutdown()
        {
            _active = false;

#if UNITY_WEBGL && !UNITY_EDITOR
            if (_useWebGLBridge)
            {
                WebGLMicrophoneBridge.StopMicrophone();
                _useWebGLBridge = false;
                _webGLPermissionPending = false;
                Debug.Log("[BreathInput] Microphone input DISABLED — WebGL recording stopped");
            }
#else
            if (Microphone.IsRecording(null))
                Microphone.End(null);

            string deviceInfo = !string.IsNullOrEmpty(_deviceName) ? $" (\"{_deviceName}\")" : "";
            Debug.Log($"[BreathInput] Microphone input DISABLED — recording stopped{deviceInfo}");
#endif

            _micClip = null;
#if !UNITY_WEBGL || UNITY_EDITOR
            _deviceName = null;
#endif
            _smoothedAmplitude = 0f;
            _smoothedIntensity = 0f;
            enabled = false;
        }

        // Calibration — record current amplitude as the floor or ceiling
        public void SetBaseline()
        {
            _calibrationBaseline = _smoothedAmplitude;
            Debug.Log($"[BreathInput] Mic calibration baseline set to {_calibrationBaseline:F4}");
        }

        public void SetMax()
        {
            _calibrationMax = _smoothedAmplitude;
            Debug.Log($"[BreathInput] Mic calibration max set to {_calibrationMax:F4}");
        }

        private void Update()
        {
            if (!_active || breathConfig == null) return;

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL: Handle async permission and get amplitude from bridge
            if (_useWebGLBridge)
            {
                if (_webGLPermissionPending)
                {
                    var permState = WebGLMicrophoneBridge.Permission;
                    if (permState == WebGLMicrophoneBridge.PermissionState.Granted)
                    {
                        _webGLPermissionPending = false;
                        Debug.Log("[BreathInput] Microphone input ENABLED — browser permission granted");
                    }
                    else if (permState == WebGLMicrophoneBridge.PermissionState.Denied)
                    {
                        _webGLPermissionPending = false;
                        _active = false;
                        string err = WebGLMicrophoneBridge.ErrorMessage;
                        Debug.LogError($"[BreathInput] Microphone input FAILED — {err}. " +
                            "Please allow microphone access in your browser and refresh the page.");
                        return;
                    }
                    else
                    {
                        // Still waiting for user to grant/deny permission
                        return;
                    }
                }

                if (!WebGLMicrophoneBridge.IsRecording)
                {
                    _smoothedIntensity = 0f;
                    return;
                }

                float rawRms = WebGLMicrophoneBridge.Amplitude;
                // WebGL doesn't have easy access to spectrum data, so skip low-freq bias
                _smoothedAmplitude = SignalProcessing.ExponentialMovingAverage(
                    _smoothedAmplitude, rawRms, breathConfig.SmoothingFactor);
            }
            else
#endif
            {
                // Desktop/Mobile: Use Unity's Microphone API
                if (_micClip == null) return;

                float rawRms = ComputeRms();
                float biasedRms = rawRms * ComputeLowFrequencyBias();

                _smoothedAmplitude = SignalProcessing.ExponentialMovingAverage(
                    _smoothedAmplitude, biasedRms, breathConfig.SmoothingFactor);
            }

            // Use runtime calibration if set, otherwise fall back to config defaults
            float baseline = _calibrationBaseline >= 0f ? _calibrationBaseline : breathConfig.CalibrationBaseline;
            float max = _calibrationMax >= 0f ? _calibrationMax : breathConfig.CalibrationMax;

            _smoothedIntensity = SignalProcessing.MapRange(_smoothedAmplitude, baseline, max, 0f, 1f);
            _smoothedIntensity = SignalProcessing.DeadZone(_smoothedIntensity, breathConfig.DeadZoneThreshold);
            _smoothedIntensity = Mathf.Clamp01(_smoothedIntensity);

            _diagTimer += Time.deltaTime;
            if (_diagTimer >= DiagIntervalSec)
            {
                _diagTimer = 0f;
#if UNITY_WEBGL && !UNITY_EDITOR
                string source = _useWebGLBridge ? "WebGL" : "Unity";
#else
                string source = "Unity";
#endif
                Debug.Log($"[BreathInput] Mic diagnostic ({source}) — raw RMS: {_smoothedAmplitude:F4}, " +
                    $"mapped intensity: {_smoothedIntensity:F3} " +
                    $"(baseline: {baseline:F4}, max: {max:F4})");
            }

            CheckLevelCrossing();
        }

        private void OnDestroy() => Shutdown();

        private float ComputeRms()
        {
#if UNITY_WEBGL
            return 0f;
#else
            int micPosition = Microphone.GetPosition(null);
            if (micPosition < sampleWindow) return 0f;

            _micClip.GetData(_sampleBuffer, micPosition - sampleWindow);

            float sum = 0f;
            for (int i = 0; i < _sampleBuffer.Length; i++)
                sum += _sampleBuffer[i] * _sampleBuffer[i];

            return Mathf.Sqrt(sum / _sampleBuffer.Length);
#endif
        }

        // Multiplier that goes up when most energy is in low-freq bins (breath-like).
        // Returns 1.0 when energy is spread out or high-freq dominant.
        private float ComputeLowFrequencyBias()
        {
            AudioListener.GetSpectrumData(_spectrumBuffer, 0, FFTWindow.BlackmanHarris);

            float lowEnergy = 0f, totalEnergy = 0f;
            for (int i = 0; i < _spectrumBuffer.Length; i++)
            {
                float e = _spectrumBuffer[i] * _spectrumBuffer[i];
                totalEnergy += e;
                if (i < lowFreqBinCount) lowEnergy += e;
            }

            if (totalEnergy <= 0f) return 1f;
            return Mathf.Lerp(1f, lowFreqWeight, lowEnergy / totalEnergy);
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
