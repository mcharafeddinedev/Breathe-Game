// SerialPort needs .NET Framework API compatibility (not .NET Standard 2.1).
// To enable: Edit > Project Settings > Player > Api Compatibility Level > .NET Framework
//   OR add BREATHE_SERIAL to Scripting Define Symbols
#if NET_4_6 || NET_UNITY_4_8 || BREATHE_SERIAL
#define SERIAL_AVAILABLE
#endif

using System;
#if SERIAL_AVAILABLE
using System.IO;
using System.IO.Ports;
using System.Threading;
#endif
using UnityEngine;
using Breathe.Data;
using Breathe.Utility;

namespace Breathe.Input
{
    public enum ComPortMode
    {
        Auto,
        Manual
    }

    public enum FanConnectionStatus
    {
        Disconnected,
        Connecting,
        Connected,
        Failed
    }

    // Reads RPM from an Arduino fan anemometer over serial.
    // A background thread does the blocking reads; main thread just grabs
    // the latest value each frame and runs it through the smoothing pipeline.
    // Compiles as a no-op stub when serial isn't available.
    // 
    // COM Port Resolution (in order):
    // 1. If Manual mode with saved port in PlayerPrefs, use that port
    // 2. If Auto mode, try last successful port first (if saved)
    // 3. If Auto mode, probe all available ports for Arduino response
    // 4. Settings UI can override at runtime
    public sealed class FanBreathInput : MonoBehaviour, IBreathInput
    {
        #region PlayerPrefs Keys
        public const string PrefKeyComPortMode = "Breathe_ComPortMode";
        public const string PrefKeyComPort = "Breathe_ComPort";
        public const string PrefKeyLastSuccessfulPort = "Breathe_LastSuccessfulComPort";
        #endregion

        [SerializeField] private BreathConfig breathConfig;

        [Header("Serial Port (Editor Default)")]
        [SerializeField, Tooltip("Fallback COM port if no saved preference exists.")]
        private string defaultComPort = "COM3";
        [SerializeField, Tooltip("Must match the Arduino sketch.")]
        private int baudRate = 9600;
        [SerializeField] private int readTimeoutMs = 100;
        [SerializeField] private int threadJoinTimeoutMs = 1000;
        [SerializeField, Tooltip("How long to wait for Arduino response when probing a port (ms).")]
        private int probeTimeoutMs = 1500;

        private string _activeComPort;
        private static FanConnectionStatus _connectionStatus = FanConnectionStatus.Disconnected;
        private static string _statusMessage = "Not initialized";
#if SERIAL_AVAILABLE && !UNITY_WEBGL
        private static string[] _cachedAvailablePorts = Array.Empty<string>();
        private static float _lastPortScanTime = -999f;
        private const float PortScanCooldown = 2f;
#endif

#if SERIAL_AVAILABLE
        private SerialPort _serialPort;
        private Thread _readThread;
        private volatile bool _threadRunning;
        private volatile float _latestRpm;
        private volatile int _lastReadMs;
#endif

        private float _smoothedIntensity;
        private int _previousLevel;
        private bool _active;
        private float _diagTimer;
        private const float DiagIntervalSec = 2f;
        private const float StaleDataTimeout = 0.5f;
        private const float SpikeRejectMultiplier = 5f;

        private float _baseline;
        private bool _calibrated;
        private float _startupTimer;
        private float _calSum;
        private int _calSamples;
        private const float SettleDuration = 2f;
        private const float CalibrationEnd = 3f;

        #region Public Status API (for Settings UI)
        public static FanConnectionStatus ConnectionStatus => _connectionStatus;
        public static string StatusMessage => _statusMessage;
        public string ActiveComPort => _activeComPort;

        public static string[] GetAvailablePorts(bool forceRefresh = false)
        {
#if SERIAL_AVAILABLE && !UNITY_WEBGL
            if (forceRefresh || Time.realtimeSinceStartup - _lastPortScanTime > PortScanCooldown)
            {
                _cachedAvailablePorts = SerialPort.GetPortNames();
                _lastPortScanTime = Time.realtimeSinceStartup;
            }
            return _cachedAvailablePorts;
#else
            return Array.Empty<string>();
#endif
        }

        public static ComPortMode GetSavedPortMode()
        {
            return (ComPortMode)PlayerPrefs.GetInt(PrefKeyComPortMode, (int)ComPortMode.Auto);
        }

        public static string GetSavedManualPort()
        {
            return PlayerPrefs.GetString(PrefKeyComPort, "");
        }

        public static void SetPortMode(ComPortMode mode)
        {
            PlayerPrefs.SetInt(PrefKeyComPortMode, (int)mode);
            PlayerPrefs.Save();
            Debug.Log($"[FanBreathInput] COM port mode set to {mode}");
        }

        public static void SetManualPort(string port)
        {
            PlayerPrefs.SetString(PrefKeyComPort, port);
            PlayerPrefs.Save();
            Debug.Log($"[FanBreathInput] Manual COM port set to {port}");
        }
        #endregion

        public float GetBreathIntensity() => _smoothedIntensity;

        public int GetBreathLevel()
        {
            return SignalProcessing.GetPowerLevel(_smoothedIntensity, breathConfig.PowerLevelThresholds);
        }

        public bool IsBreathing() => _smoothedIntensity > breathConfig.DeadZoneThreshold;

        public void Initialize()
        {
            _smoothedIntensity = 0f;
            _previousLevel = 0;
            _baseline = 0f;
            _calibrated = false;
            _startupTimer = 0f;
            _calSum = 0f;
            _calSamples = 0;
            _activeComPort = null;
            _connectionStatus = FanConnectionStatus.Connecting;
            _statusMessage = "Initializing...";

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.LogWarning("[BreathInput] Fan (USB serial) is not available in WebGL builds — browsers cannot open COM ports " +
                "like the desktop player. Use Simulated (or a desktop/mobile build) for real hardware.");
            _connectionStatus = FanConnectionStatus.Failed;
            _statusMessage = "Not available in browser";
            _active = false;
#elif SERIAL_AVAILABLE
            _latestRpm = 0f;
            _lastReadMs = System.Environment.TickCount;

            string[] availablePorts = GetAvailablePorts(forceRefresh: true);
            Debug.Log($"[BreathInput] Fan hardware — scanning serial ports... found: [{string.Join(", ", availablePorts)}]");

            if (availablePorts.Length == 0)
            {
                Debug.LogError("[BreathInput] Custom hardware device NOT FOUND — no serial ports detected. " +
                    "Please connect the fan anemometer via USB and restart.");
                _connectionStatus = FanConnectionStatus.Failed;
                _statusMessage = "No serial ports found";
                _active = false;
                return;
            }

            string portToUse = ResolveComPort(availablePorts);

            if (string.IsNullOrEmpty(portToUse))
            {
                Debug.LogError("[BreathInput] Custom hardware device NOT FOUND — could not find Arduino on any port. " +
                    $"Available ports: [{string.Join(", ", availablePorts)}]. " +
                    "Try selecting a specific port in Settings > Input > COM Port.");
                _connectionStatus = FanConnectionStatus.Failed;
                _statusMessage = "Arduino not detected";
                _active = false;
                return;
            }

            if (!TryConnectToPort(portToUse))
            {
                _connectionStatus = FanConnectionStatus.Failed;
                _active = false;
                return;
            }

            _activeComPort = portToUse;
            PlayerPrefs.SetString(PrefKeyLastSuccessfulPort, portToUse);
            PlayerPrefs.Save();

            _connectionStatus = FanConnectionStatus.Connected;
            _statusMessage = $"Connected on {portToUse}";
            _active = true;
            enabled = true;
            Debug.Log($"[BreathInput] Fan hardware input ENABLED — connected on {portToUse} @ {baudRate} baud");
#else
            Debug.LogError("[BreathInput] Custom hardware device NOT AVAILABLE — serial support requires .NET Framework " +
                "API compatibility level or the BREATHE_SERIAL scripting define. " +
                "Set in Edit > Project Settings > Player > Api Compatibility Level > .NET Framework.");
            _connectionStatus = FanConnectionStatus.Failed;
            _statusMessage = "Serial not available";
            _active = false;
#endif
        }

#if SERIAL_AVAILABLE
        private string ResolveComPort(string[] availablePorts)
        {
            ComPortMode mode = GetSavedPortMode();
            string savedManualPort = GetSavedManualPort();
            string lastSuccessfulPort = PlayerPrefs.GetString(PrefKeyLastSuccessfulPort, "");

            Debug.Log($"[BreathInput] Port resolution — mode: {mode}, manual: '{savedManualPort}', lastSuccess: '{lastSuccessfulPort}'");

            if (mode == ComPortMode.Manual && !string.IsNullOrEmpty(savedManualPort))
            {
                bool exists = Array.Exists(availablePorts,
                    p => string.Equals(p, savedManualPort, StringComparison.OrdinalIgnoreCase));
                if (exists)
                {
                    Debug.Log($"[BreathInput] Using manually configured port: {savedManualPort}");
                    return savedManualPort;
                }
                Debug.LogWarning($"[BreathInput] Manually configured port {savedManualPort} not available, falling back to auto-detect");
            }

            if (!string.IsNullOrEmpty(lastSuccessfulPort))
            {
                bool exists = Array.Exists(availablePorts,
                    p => string.Equals(p, lastSuccessfulPort, StringComparison.OrdinalIgnoreCase));
                if (exists && ProbePortForArduino(lastSuccessfulPort))
                {
                    Debug.Log($"[BreathInput] Last successful port {lastSuccessfulPort} responded — using it");
                    return lastSuccessfulPort;
                }
            }

            Debug.Log($"[BreathInput] Auto-detecting Arduino on {availablePorts.Length} port(s)...");
            foreach (string port in availablePorts)
            {
                if (string.Equals(port, lastSuccessfulPort, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (ProbePortForArduino(port))
                {
                    Debug.Log($"[BreathInput] Arduino detected on {port}");
                    return port;
                }
            }

            bool defaultExists = Array.Exists(availablePorts,
                p => string.Equals(p, defaultComPort, StringComparison.OrdinalIgnoreCase));
            if (defaultExists)
            {
                Debug.Log($"[BreathInput] No Arduino response detected, falling back to default port: {defaultComPort}");
                return defaultComPort;
            }

            if (availablePorts.Length > 0)
            {
                Debug.Log($"[BreathInput] No Arduino response detected, using first available port: {availablePorts[0]}");
                return availablePorts[0];
            }

            return null;
        }

        private bool ProbePortForArduino(string port)
        {
            SerialPort probePort = null;
            try
            {
                probePort = new SerialPort(port, baudRate)
                {
                    ReadTimeout = probeTimeoutMs,
                    DtrEnable = true
                };
                probePort.Open();

                Thread.Sleep(100);
                probePort.DiscardInBuffer();

                int startTime = Environment.TickCount;
                while (Environment.TickCount - startTime < probeTimeoutMs)
                {
                    try
                    {
                        string line = probePort.ReadLine()?.Trim();
                        if (line != null && line.StartsWith("RPM:", StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.Log($"[BreathInput] Probe success on {port}: received '{line}'");
                            probePort.Close();
                            probePort.Dispose();
                            return true;
                        }
                    }
                    catch (TimeoutException)
                    {
                        break;
                    }
                }

                probePort.Close();
                probePort.Dispose();
                return false;
            }
            catch (Exception ex)
            {
                Debug.Log($"[BreathInput] Probe failed on {port}: {ex.Message}");
                if (probePort != null)
                {
                    try { if (probePort.IsOpen) probePort.Close(); } catch { }
                    probePort.Dispose();
                }
                return false;
            }
        }

        private bool TryConnectToPort(string port)
        {
            try
            {
                _serialPort = new SerialPort(port, baudRate)
                {
                    ReadTimeout = readTimeoutMs,
                    DtrEnable = true
                };
                _serialPort.Open();

                _threadRunning = true;
                _readThread = new Thread(ReadSerialLoop)
                {
                    IsBackground = true,
                    Name = "FanBreathReader"
                };
                _readThread.Start();

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                Debug.LogError($"[BreathInput] Custom hardware device FAILED — {port} is in use by another application. " +
                    "Close any other serial monitors (Arduino IDE, PuTTY, etc.) and try again.");
                _statusMessage = $"{port} in use by another app";
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BreathInput] Custom hardware device FAILED — could not open {port}: {ex.Message}");
                _statusMessage = $"Failed to open {port}";
                return false;
            }
        }
#endif

        public void Reinitialize()
        {
            Shutdown();
            Initialize();
        }

        public void Shutdown()
        {
            _active = false;

#if SERIAL_AVAILABLE
            _threadRunning = false;

            if (_readThread != null && _readThread.IsAlive)
            {
                if (!_readThread.Join(threadJoinTimeoutMs))
                    Debug.LogWarning("[BreathInput] Fan hardware — reader thread didn't stop in time, may still be blocking.");
                _readThread = null;
            }

            if (_serialPort != null)
            {
                bool wasOpen = _serialPort.IsOpen;
                try { if (wasOpen) _serialPort.Close(); }
                catch (IOException ex) { Debug.LogWarning($"[BreathInput] Fan hardware — error closing port: {ex.Message}"); }
                _serialPort.Dispose();
                _serialPort = null;

                Debug.Log($"[BreathInput] Fan hardware input DISABLED — serial port {_activeComPort ?? "unknown"} closed");
            }
            else
            {
                Debug.Log("[BreathInput] Fan hardware input DISABLED");
            }
#else
            Debug.Log("[BreathInput] Fan hardware input DISABLED");
#endif
            _smoothedIntensity = 0f;
            _connectionStatus = FanConnectionStatus.Disconnected;
            _statusMessage = "Disconnected";
            _activeComPort = null;
            enabled = false;
        }

        private void Update()
        {
            if (!_active || breathConfig == null) return;

#if SERIAL_AVAILABLE
            float raw = _latestRpm;
            float secsSinceRead = (System.Environment.TickCount - _lastReadMs) / 1000f;
            if (secsSinceRead > StaleDataTimeout)
                raw = _baseline;
#else
            float raw = 0f;
#endif

            if (!_calibrated)
            {
                _startupTimer += Time.deltaTime;
                if (_startupTimer > SettleDuration)
                {
                    _calSum += raw;
                    _calSamples++;
                }
                if (_startupTimer >= CalibrationEnd && _calSamples > 0)
                {
                    _baseline = _calSum / _calSamples;
                    _calibrated = true;
                    Debug.Log($"[BreathInput] Fan calibrated — rest baseline: {_baseline:F0} " +
                        $"({_calSamples} samples over {CalibrationEnd - SettleDuration:F0}s)");
                }
                _smoothedIntensity = 0f;
                return;
            }

            float deviation = Mathf.Abs(raw - _baseline);

            float spikeThreshold = breathConfig.MaxExpectedRPM * SpikeRejectMultiplier;
            bool spiked = deviation > spikeThreshold;

            if (!spiked)
            {
                float rawIntensity = Mathf.Clamp01(deviation / breathConfig.MaxExpectedRPM);

                _smoothedIntensity = SignalProcessing.ExponentialMovingAverage(
                    _smoothedIntensity, rawIntensity, breathConfig.SmoothingFactor);
            }
            else
            {
                _smoothedIntensity = SignalProcessing.ExponentialMovingAverage(
                    _smoothedIntensity, 0f, breathConfig.SmoothingFactor);
            }

            _smoothedIntensity = SignalProcessing.DeadZone(
                _smoothedIntensity, breathConfig.DeadZoneThreshold);
            _smoothedIntensity = Mathf.Clamp01(_smoothedIntensity);

            _diagTimer += Time.deltaTime;
            if (_diagTimer >= DiagIntervalSec)
            {
                _diagTimer = 0f;
                string tag = spiked ? " [SPIKE REJECTED]" : "";
                Debug.Log($"[BreathInput] Fan diagnostic — raw: {raw:F0}, baseline: {_baseline:F0}, " +
                    $"deviation: {deviation:F0}, smoothed: {_smoothedIntensity:F3} " +
                    $"(max: {breathConfig.MaxExpectedRPM:F0}){tag}");
            }

            CheckLevelCrossing();
        }

        private void OnDestroy() => Shutdown();

#if SERIAL_AVAILABLE
        private void ReadSerialLoop()
        {
            int consecutiveErrors = 0;
            const int maxConsecutiveErrors = 20;

            while (_threadRunning)
            {
                try
                {
                    if (_serialPort == null || !_serialPort.IsOpen) break;

                    string line = _serialPort.ReadLine()?.Trim();
                    if (line != null && line.StartsWith("RPM:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (float.TryParse(line.Substring(4),
                                System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out float rpm))
                        {
                            _latestRpm = Mathf.Max(0f, rpm);
                            _lastReadMs = System.Environment.TickCount;
                            consecutiveErrors = 0;
                        }
                    }
                }
                catch (TimeoutException)
                {
                    // Normal — no data within timeout window, just loop again
                }
                catch (IOException)
                {
                    consecutiveErrors++;
                    if (consecutiveErrors >= maxConsecutiveErrors)
                    {
                        Debug.LogWarning($"[BreathInput] Fan serial: {maxConsecutiveErrors} consecutive IO errors — " +
                            "connection may be lost. Check USB/pin connection.");
                        consecutiveErrors = 0;
                    }
                    Thread.Sleep(50);
                }
                catch (InvalidOperationException)
                {
                    // Port was closed externally
                    break;
                }
            }
        }
#endif

        private void CheckLevelCrossing()
        {
            int level = GetBreathLevel();
            if (level != _previousLevel)
            {
                Debug.Log($"[BreathInput] Fan intensity level changed to {level}");
                _previousLevel = level;
            }
        }
    }
}
