// SerialPort requires .NET Framework API compatibility (not .NET Standard 2.1).
// To enable fan hardware support:
//   1. Edit > Project Settings > Player > Other Settings > Api Compatibility Level → .NET Framework
//      (this defines NET_4_6 or NET_UNITY_4_8 automatically)
//   OR add BREATHE_SERIAL to: Project Settings > Player > Scripting Define Symbols
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
    /// <summary>
    /// Reads RPM telemetry from an Arduino-driven fan anemometer over a serial
    /// (COM) port. A background thread handles blocking serial reads; the main
    /// thread consumes the latest value each frame and applies the standard
    /// smoothing / dead-zone / mapping pipeline.
    ///
    /// Requires .NET Framework API compatibility level or the System.IO.Ports
    /// NuGet package. On platforms where serial is unavailable, this class
    /// compiles as a no-op stub.
    /// </summary>
    public sealed class FanBreathInput : MonoBehaviour, IBreathInput
    {
        [Header("Configuration")]
        [SerializeField, Tooltip("Shared breath-processing parameters.")]
        private BreathConfig breathConfig;

        [Header("Serial Port")]
        [SerializeField, Tooltip("COM port the Arduino is connected to (e.g. COM3).")]
        private string comPort = "COM3";

        [SerializeField, Tooltip("Serial baud rate. Must match the Arduino sketch.")]
        private int baudRate = 9600;

        [SerializeField, Tooltip("Timeout in milliseconds for serial read operations.")]
        private int readTimeoutMs = 100;

        [Header("Thread Control")]
        [SerializeField, Tooltip("Maximum milliseconds to wait for the reader thread " +
            "to terminate during Shutdown.")]
        private int threadJoinTimeoutMs = 1000;

#if SERIAL_AVAILABLE
        private SerialPort _serialPort;
        private Thread _readThread;
        private volatile bool _threadRunning;
        private volatile float _latestRpm;
#endif

        private float _smoothedIntensity;
        private int _previousLevel;
        private bool _active;

        // ------------------------------------------------------------------ IBreathInput

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
            Debug.LogWarning("[BreathInput] Serial port not available on this platform. " +
                "Set Api Compatibility Level to .NET Framework for fan hardware support.");
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
                    Debug.LogWarning("[BreathInput] Fan serial: reader thread did not terminate in time.");
                _readThread = null;
            }

            if (_serialPort != null)
            {
                try
                {
                    if (_serialPort.IsOpen) _serialPort.Close();
                }
                catch (IOException ex)
                {
                    Debug.LogWarning("[BreathInput] Fan serial: " + ex.Message);
                }
                _serialPort.Dispose();
                _serialPort = null;
            }
#endif

            _smoothedIntensity = 0f;
            enabled = false;
        }

        // ------------------------------------------------------------------ Unity lifecycle

        private void Update()
        {
            if (!_active || breathConfig == null) return;

#if SERIAL_AVAILABLE
            float rpm = _latestRpm;
#else
            float rpm = 0f;
#endif

            float rawIntensity = SignalProcessing.MapRange(
                rpm, 0f, breathConfig.MaxExpectedRPM, 0f, 1f);

            _smoothedIntensity = SignalProcessing.ExponentialMovingAverage(
                _smoothedIntensity, rawIntensity, breathConfig.SmoothingFactor);

            _smoothedIntensity = SignalProcessing.DeadZone(
                _smoothedIntensity, breathConfig.DeadZoneThreshold);

            _smoothedIntensity = Mathf.Clamp01(_smoothedIntensity);

            CheckLevelCrossing();
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        // ------------------------------------------------------------------ Background thread

#if SERIAL_AVAILABLE
        private void ReadSerialLoop()
        {
            while (_threadRunning)
            {
                try
                {
                    if (_serialPort == null || !_serialPort.IsOpen) break;

                    string line = _serialPort.ReadLine();
                    if (line == null) continue;

                    line = line.Trim();
                    if (line.StartsWith("RPM:", StringComparison.OrdinalIgnoreCase))
                    {
                        string rpmStr = line.Substring(4);
                        if (float.TryParse(rpmStr, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out float rpm))
                        {
                            _latestRpm = Mathf.Max(0f, rpm);
                        }
                    }
                }
                catch (TimeoutException)
                {
                    // Expected when no data is available within the read timeout.
                }
                catch (IOException ex)
                {
                    Debug.LogWarning("[BreathInput] Fan serial: " + ex.Message);
                    _latestRpm = 0f;
                    break;
                }
                catch (InvalidOperationException)
                {
                    break;
                }
            }
        }
#endif

        // ------------------------------------------------------------------ Internal

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
