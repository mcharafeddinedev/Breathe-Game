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
    // Reads RPM from an Arduino fan anemometer over serial.
    // A background thread does the blocking reads; main thread just grabs
    // the latest value each frame and runs it through the smoothing pipeline.
    // Compiles as a no-op stub when serial isn't available.
    public sealed class FanBreathInput : MonoBehaviour, IBreathInput
    {
        [SerializeField] private BreathConfig breathConfig;

        [Header("Serial Port")]
        [SerializeField, Tooltip("COM port the Arduino is on (e.g. COM3).")]
        private string comPort = "COM3";
        [SerializeField, Tooltip("Must match the Arduino sketch.")]
        private int baudRate = 9600;
        [SerializeField] private int readTimeoutMs = 100;
        [SerializeField] private int threadJoinTimeoutMs = 1000;

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

#if SERIAL_AVAILABLE
            _latestRpm = 0f;
            _lastReadMs = System.Environment.TickCount;

            string[] availablePorts = SerialPort.GetPortNames();
            Debug.Log($"[BreathInput] Fan hardware — scanning serial ports... found: [{string.Join(", ", availablePorts)}]");

            if (availablePorts.Length == 0)
            {
                Debug.LogError("[BreathInput] Custom hardware device NOT FOUND — no serial ports detected. " +
                    "Please connect the fan anemometer via USB and restart.");
                _active = false;
                return;
            }

            bool portExists = System.Array.Exists(availablePorts,
                p => string.Equals(p, comPort, StringComparison.OrdinalIgnoreCase));
            if (!portExists)
            {
                Debug.LogError($"[BreathInput] Custom hardware device NOT FOUND — configured port {comPort} is not available. " +
                    $"Available ports: [{string.Join(", ", availablePorts)}]. " +
                    "Check your USB connection or update the COM port in the Inspector.");
                _active = false;
                return;
            }

            try
            {
                _serialPort = new SerialPort(comPort, baudRate)
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

                _active = true;
                enabled = true;
                Debug.Log($"[BreathInput] Fan hardware input ENABLED — connected on {comPort} @ {baudRate} baud");
            }
            catch (UnauthorizedAccessException)
            {
                Debug.LogError($"[BreathInput] Custom hardware device FAILED — {comPort} is in use by another application. " +
                    "Close any other serial monitors (Arduino IDE, PuTTY, etc.) and try again.");
                _active = false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BreathInput] Custom hardware device FAILED — could not open {comPort}: {ex.Message}");
                _active = false;
            }
#else
            Debug.LogError("[BreathInput] Custom hardware device NOT AVAILABLE — serial support requires .NET Framework " +
                "API compatibility level or the BREATHE_SERIAL scripting define. " +
                "Set in Edit > Project Settings > Player > Api Compatibility Level > .NET Framework.");
            _active = false;
#endif
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

                Debug.Log($"[BreathInput] Fan hardware input DISABLED — serial port {comPort} closed");
            }
            else
            {
                Debug.Log("[BreathInput] Fan hardware input DISABLED");
            }
#else
            Debug.Log("[BreathInput] Fan hardware input DISABLED");
#endif
            _smoothedIntensity = 0f;
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
