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
#endif

        private float _smoothedIntensity;
        private int _previousLevel;
        private bool _active;

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

#if SERIAL_AVAILABLE
            _latestRpm = 0f;
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
                Debug.Log($"[BreathInput] Fan serial opened on {comPort} @ {baudRate} baud");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BreathInput] Fan serial: " + ex.Message);
                _active = false;
            }
#else
            Debug.LogWarning("[BreathInput] Serial not available — need .NET Framework for fan hardware.");
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
                    Debug.LogWarning("[BreathInput] Fan serial: reader thread didn't stop in time.");
                _readThread = null;
            }

            if (_serialPort != null)
            {
                try { if (_serialPort.IsOpen) _serialPort.Close(); }
                catch (IOException ex) { Debug.LogWarning("[BreathInput] Fan serial: " + ex.Message); }
                _serialPort.Dispose();
                _serialPort = null;
            }
#endif
            _smoothedIntensity = 0f;
            enabled = false;
        }

        private void Update()
        {
            if (!_active || breathConfig == null) return;

#if SERIAL_AVAILABLE
            float rpm = _latestRpm;
#else
            float rpm = 0f;
#endif

            float rawIntensity = SignalProcessing.MapRange(rpm, 0f, breathConfig.MaxExpectedRPM, 0f, 1f);
            _smoothedIntensity = SignalProcessing.ExponentialMovingAverage(
                _smoothedIntensity, rawIntensity, breathConfig.SmoothingFactor);
            _smoothedIntensity = SignalProcessing.DeadZone(_smoothedIntensity, breathConfig.DeadZoneThreshold);
            _smoothedIntensity = Mathf.Clamp01(_smoothedIntensity);

            CheckLevelCrossing();
        }

        private void OnDestroy() => Shutdown();

#if SERIAL_AVAILABLE
        // Background thread — blocks on serial reads
        private void ReadSerialLoop()
        {
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
                        }
                    }
                }
                catch (TimeoutException) { } // normal — no data within timeout
                catch (IOException ex)
                {
                    Debug.LogWarning("[BreathInput] Fan serial: " + ex.Message);
                    _latestRpm = 0f;
                    break;
                }
                catch (InvalidOperationException) { break; }
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
