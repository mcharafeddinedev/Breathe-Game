using System;
using UnityEngine;
using Breathe.Data;
using Breathe.Utility;

namespace Breathe.Input
{
    public enum MicConnectionStatus
    {
        Disconnected,
        Connecting,
        Connected,
        PermissionPending,
        PermissionDenied,
        Failed
    }

    // Reads breath from a selected microphone. Computes RMS amplitude,
    // applies a low-frequency bias (breath is mostly low freq, unlike speech),
    // then smooths + maps through a calibrated range to get a 0-1 signal.
    //
    // Platform support:
    // - Desktop/Mobile: Uses Unity's Microphone class
    // - WebGL: Uses WebGLMicrophoneBridge (Web Audio API via jslib plugin)
    public sealed class MicBreathInput : MonoBehaviour, IBreathInput
    {
        #region PlayerPrefs Keys
        public const string PrefKeyMicDevice = "Breathe_MicDevice";
        #endregion

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

        #region Static Status/Device API (for Settings UI)
        private static MicConnectionStatus _connectionStatus = MicConnectionStatus.Disconnected;
        private static string _statusMessage = "Not initialized";
        private static string _activeDeviceName = "";
        private static string[] _cachedDevices = Array.Empty<string>();
        private static float _lastDeviceScanTime = -999f;
        private const float DeviceScanCooldown = 1f;
        private static float _currentIntensity;
        private static MicBreathInput _activeInstance;

        public static MicConnectionStatus ConnectionStatus => _connectionStatus;
        public static string StatusMessage => _statusMessage;
        public static string ActiveDeviceName => _activeDeviceName;
        /// <summary>Current mic intensity (0–1) for live level display in Settings. Updated when mic is recording.</summary>
        public static float CurrentIntensity => _currentIntensity;
        public static MicBreathInput ActiveInstance => _activeInstance;

        public static string[] GetAvailableDevices(bool forceRefresh = false)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Array.Empty<string>();
#else
            if (forceRefresh || Time.realtimeSinceStartup - _lastDeviceScanTime > DeviceScanCooldown)
            {
                _cachedDevices = Microphone.devices;
                _lastDeviceScanTime = Time.realtimeSinceStartup;
            }
            return _cachedDevices;
#endif
        }

        public static string GetSavedDevice()
        {
            return PlayerPrefs.GetString(PrefKeyMicDevice, "");
        }

        public static void SetSavedDevice(string deviceName)
        {
            PlayerPrefs.SetString(PrefKeyMicDevice, deviceName ?? "");
            PlayerPrefs.Save();
            Debug.Log($"[MicBreathInput] Saved microphone device: \"{deviceName ?? "(default)"}\"");
        }

        public static int GetDeviceIndex(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName)) return -1;
            var devices = GetAvailableDevices();
            return Array.FindIndex(devices, d => string.Equals(d, deviceName, StringComparison.OrdinalIgnoreCase));
        }
        #endregion

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
        public bool IsActive => _active;

        public int GetBreathLevel()
        {
            return SignalProcessing.GetPowerLevel(_smoothedIntensity, breathConfig.PowerLevelThresholds);
        }

        public bool IsBreathing() => _smoothedIntensity > breathConfig.DeadZoneThreshold;

        /// <summary>Stops current mic session (if any) and reinitializes with current saved device. Call from Settings after device change.</summary>
        public void Reinitialize()
        {
            Shutdown();
            Initialize();
        }

        public void Initialize()
        {
            _smoothedAmplitude = 0f;
            _smoothedIntensity = 0f;
            _previousLevel = 0;
            _calibrationBaseline = -1f;
            _calibrationMax = -1f;
            _connectionStatus = MicConnectionStatus.Connecting;
            _statusMessage = "Initializing...";
            _activeDeviceName = "";

#if UNITY_WEBGL && !UNITY_EDITOR
            _useWebGLBridge = false;
            _webGLPermissionPending = false;
            // WebGL: Use Web Audio API bridge instead of Unity's Microphone class
            if (!WebGLMicrophoneBridge.IsSupported)
            {
                Debug.LogError("[BreathInput] Microphone input FAILED — this browser does not support getUserMedia. " +
                    "Try a modern browser like Chrome, Firefox, or Edge.");
                _connectionStatus = MicConnectionStatus.Failed;
                _statusMessage = "Browser not supported";
                _active = false;
                return;
            }

            _useWebGLBridge = true;
            _webGLPermissionPending = true;
            _connectionStatus = MicConnectionStatus.PermissionPending;
            _statusMessage = "Waiting for permission...";

            if (!WebGLMicrophoneBridge.StartMicrophone())
            {
                Debug.LogError("[BreathInput] Microphone input FAILED — could not request microphone access.");
                _connectionStatus = MicConnectionStatus.Failed;
                _statusMessage = "Could not request access";
                _active = false;
                return;
            }

            // Note: Permission is async in browsers. We'll check status in Update().
            _active = true;
            enabled = true;
            Debug.Log("[BreathInput] Microphone input INITIALIZING — waiting for browser permission...");
#else
            // Desktop/Mobile: Use Unity's native Microphone API
            string[] devices = GetAvailableDevices(forceRefresh: true);
            if (devices.Length == 0)
            {
                Debug.LogError("[BreathInput] Microphone input FAILED — no microphone detected on this system. " +
                    "Please connect a microphone and restart.");
                _connectionStatus = MicConnectionStatus.Failed;
                _statusMessage = "No microphone found";
                _active = false;
                return;
            }

            // Resolve device: use saved preference if valid, otherwise first available device
            string savedDevice = GetSavedDevice();
            string deviceToUse = devices[0]; // Default to first device
            if (!string.IsNullOrEmpty(savedDevice))
            {
                int idx = GetDeviceIndex(savedDevice);
                if (idx >= 0)
                    deviceToUse = savedDevice;
                else
                    Debug.LogWarning($"[MicBreathInput] Saved device \"{savedDevice}\" not found, using first available: \"{devices[0]}\"");
            }

            _deviceName = deviceToUse;
            _activeDeviceName = _deviceName;
            // Use explicit device name (not null) so GetPosition() works correctly
            _micClip = Microphone.Start(_deviceName, true, 1, sampleRate);

            if (_micClip == null)
            {
                Debug.LogError($"[BreathInput] Microphone input FAILED — could not start recording on device \"{_deviceName}\". " +
                    "The device may be in use by another application.");
                _connectionStatus = MicConnectionStatus.Failed;
                _statusMessage = $"Could not open \"{_deviceName}\"";
                _active = false;
                return;
            }

            _sampleBuffer = new float[sampleWindow];
            _spectrumBuffer = new float[spectrumSize];
            _active = true;
            _activeInstance = this;
            enabled = true;
            _connectionStatus = MicConnectionStatus.Connected;
            _statusMessage = "Listening";

            int deviceCount = devices.Length;
            Debug.Log($"[BreathInput] Microphone input ENABLED — using device: \"{_deviceName}\" @ {sampleRate}Hz " +
                $"({deviceCount} device{(deviceCount > 1 ? "s" : "")} available)");
#endif
        }

        public void Shutdown()
        {
            _active = false;
            _currentIntensity = 0f;
            if (_activeInstance == this) _activeInstance = null;
            _connectionStatus = MicConnectionStatus.Disconnected;
            _statusMessage = "Stopped";

#if UNITY_WEBGL && !UNITY_EDITOR
            if (_useWebGLBridge)
            {
                WebGLMicrophoneBridge.StopMicrophone();
                _useWebGLBridge = false;
                _webGLPermissionPending = false;
                Debug.Log("[BreathInput] Microphone input DISABLED — WebGL recording stopped");
            }
#else
            if (!string.IsNullOrEmpty(_deviceName) && Microphone.IsRecording(_deviceName))
                Microphone.End(_deviceName);

            string deviceInfo = !string.IsNullOrEmpty(_deviceName) ? $" (\"{_deviceName}\")" : "";
            Debug.Log($"[BreathInput] Microphone input DISABLED — recording stopped{deviceInfo}");

            _deviceName = null;
#endif

            _micClip = null;
            _activeDeviceName = "";
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
                        _connectionStatus = MicConnectionStatus.Connected;
                        _statusMessage = "Listening (browser)";
                        _activeDeviceName = "(Browser Mic)";
                        Debug.Log("[BreathInput] Microphone input ENABLED — browser permission granted");
                    }
                    else if (permState == WebGLMicrophoneBridge.PermissionState.Denied)
                    {
                        _webGLPermissionPending = false;
                        _active = false;
                        _connectionStatus = MicConnectionStatus.PermissionDenied;
                        string err = WebGLMicrophoneBridge.ErrorMessage;
                        _statusMessage = string.IsNullOrEmpty(err) ? "Permission denied" : err;
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

                const float MicGain = 3f; // Amplification factor for mic input sensitivity
                float rawRms = ComputeRms();
                float biasedRms = rawRms * ComputeLowFrequencyBias() * MicGain;

                _smoothedAmplitude = SignalProcessing.ExponentialMovingAverage(
                    _smoothedAmplitude, biasedRms, breathConfig.SmoothingFactor);
            }

            // Use runtime calibration if set, otherwise fall back to config defaults
            float baseline = _calibrationBaseline >= 0f ? _calibrationBaseline : breathConfig.CalibrationBaseline;
            float max = _calibrationMax >= 0f ? _calibrationMax : breathConfig.CalibrationMax;

            _smoothedIntensity = SignalProcessing.MapRange(_smoothedAmplitude, baseline, max, 0f, 1f);
            _smoothedIntensity = SignalProcessing.DeadZone(_smoothedIntensity, breathConfig.DeadZoneThreshold);
            _smoothedIntensity = Mathf.Clamp01(_smoothedIntensity);
            _currentIntensity = _smoothedIntensity;

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
            // Use the same device name that was passed to Microphone.Start()
            int micPosition = Microphone.GetPosition(_deviceName);
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
