using UnityEngine;
using Breathe.Utility;

namespace Breathe.Audio
{
    /// <summary>
    /// Balloon inflation: single layer of soft filtered noise. Pitch rises with inflation.
    /// No secondary tones, squeaks, or completion beeps.
    /// Desktop/Editor: uses OnAudioFilterRead for real-time synthesis.
    /// WebGL: uses pre-generated noise loop with volume/pitch modulation.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class ProceduralBalloonAudio : MonoBehaviour
    {
        [Header("Volume")]
        [SerializeField, Range(0f, 1f)] float _masterVolume = 1f;
#if !UNITY_WEBGL || UNITY_EDITOR
        [SerializeField, Range(0f, 1f)] float _airRushVolume = 0.65f;

        [Header("Air rush")]
        [SerializeField, Tooltip("Base cutoff (Hz).")]
        float _airRushBaseFreq = 420f;
        [SerializeField, Tooltip("Higher pitch as balloon fills.")]
        float _airRushInflationFreqBoost = 860f;
        [SerializeField, Tooltip("Extra cutoff from breath.")]
        float _airRushBreathFreqBoost = 175f;
#endif

        [Header("Smoothing")]
        [SerializeField] float _breathSmoothing = 10f;
        [SerializeField] float _inflationSmoothing = 4f;

        float _currentBreathPower;
        float _currentInflation;
        float _targetBreathPower;
        float _targetInflation;
        bool _isActive;
        float _cachedSfxLinear = 1f; // Cached on main thread for audio thread access

        AudioSource _audioSource;
        int _sampleRate;

#if !UNITY_WEBGL || UNITY_EDITOR
        double _noisePhase;
        float _noiseFilterState;
        float _noiseFilterCascade;
        float _noiseFilterBloom;
        System.Random _noiseRng;
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        AudioClip _webglNoiseClip;
        const int WebGLLoopSamples = 44100; // 1 second loop
        const int WebGLSampleRate = 44100;
#endif

        void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.loop = true;
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
            _audioSource.volume = _masterVolume;

            _sampleRate = AudioSettings.outputSampleRate;
#if !UNITY_WEBGL || UNITY_EDITOR
            _noiseRng = new System.Random(42);
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
            GenerateWebGLNoiseLoop();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        void GenerateWebGLNoiseLoop()
        {
            _webglNoiseClip = AudioClip.Create("BalloonNoiseLoop", WebGLLoopSamples, 1, WebGLSampleRate, false);
            float[] samples = new float[WebGLLoopSamples];
            
            var rng = new System.Random(42);
            float filterState = 0f;
            float filterCascade = 0f;
            float dt = 1f / WebGLSampleRate;
            float cutoffHz = 600f; // Mid-range filtered noise
            float rc = 1f / (2f * Mathf.PI * cutoffHz);
            float alpha = dt / (rc + dt);
            
            for (int i = 0; i < WebGLLoopSamples; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.58f;
                filterState += alpha * (white - filterState);
                filterCascade += alpha * (filterState * 0.92f - filterCascade);
                samples[i] = filterCascade * 0.7f;
            }
            
            _webglNoiseClip.SetData(samples, 0);
            _audioSource.clip = _webglNoiseClip;
        }
#endif

        void Update()
        {
            // Cache SFX volume on main thread for audio thread access
            _cachedSfxLinear = SfxLinear();

            _currentBreathPower = Mathf.Lerp(_currentBreathPower, _targetBreathPower,
                Time.deltaTime * _breathSmoothing);
            _currentInflation = Mathf.Lerp(_currentInflation, _targetInflation,
                Time.deltaTime * _inflationSmoothing);

            if (_isActive && _currentBreathPower > 0.01f)
            {
                if (!_audioSource.isPlaying)
                    _audioSource.Play();
            }
            else if (_audioSource.isPlaying && _currentBreathPower < 0.01f)
            {
                _audioSource.Stop();
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL: modulate volume and pitch based on breath/inflation
            if (_audioSource.isPlaying)
            {
                float breathCurve = Mathf.Sqrt(Mathf.Clamp01(_currentBreathPower)) * Mathf.Lerp(0.55f, 1f, _currentBreathPower);
                _audioSource.volume = 0.65f * breathCurve * _masterVolume * _cachedSfxLinear;
                // Pitch rises with inflation (0.8 to 1.4 range)
                _audioSource.pitch = 0.8f + _currentInflation * 0.6f + _currentBreathPower * 0.15f;
            }
#endif
        }

        public void UpdateAudio(float breathPower, float inflationProgress, bool active)
        {
            _targetBreathPower = Mathf.Clamp01(breathPower);
            _targetInflation = Mathf.Clamp01(inflationProgress);
            _isActive = active;

            if (!active)
                _targetBreathPower = 0f;
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (!_isActive && _currentBreathPower < 0.001f)
            {
                System.Array.Clear(data, 0, data.Length);
                return;
            }

            double sampleDelta = 1.0 / _sampleRate;
            float breath = _currentBreathPower;
            float inflation = _currentInflation;

            float airFreq = _airRushBaseFreq + inflation * _airRushInflationFreqBoost + breath * _airRushBreathFreqBoost;
            float breathCurve = Mathf.Sqrt(Mathf.Clamp01(breath)) * Mathf.Lerp(0.55f, 1f, breath);
            float airVol = _airRushVolume * breathCurve * _masterVolume * _cachedSfxLinear;

            for (int i = 0; i < data.Length; i += channels)
            {
                float sample = 0f;

                if (breath > 0.002f)
                {
                    float noise = GenerateBreathNoise(airFreq, breath);
                    sample += noise * airVol;
                }

                sample = SoftClip(sample);

                for (int c = 0; c < channels; c++)
                    data[i + c] = sample;
            }
        }

        float GenerateBreathNoise(float cutoffHz, float intensity)
        {
            float white = (float)(_noiseRng.NextDouble() * 2.0 - 1.0) * 0.58f;

            float rc = 1f / (2f * Mathf.PI * Mathf.Max(120f, cutoffHz));
            float dt = 1f / _sampleRate;
            float alpha = dt / (rc + dt);
            _noiseFilterState += alpha * (white - _noiseFilterState);

            float rc2 = 1f / (2f * Mathf.PI * Mathf.Max(140f, cutoffHz * 0.85f));
            float alpha2 = dt / (rc2 + dt);
            _noiseFilterCascade += alpha2 * (_noiseFilterState * 0.92f - _noiseFilterCascade);

            float rc3 = 1f / (2f * Mathf.PI * Mathf.Min(3400f, cutoffHz + 900f));
            float alpha3 = dt / (rc3 + dt);
            _noiseFilterBloom += alpha3 * (white * 0.12f - _noiseFilterBloom);

            float turbulence = Mathf.Sin((float)_noisePhase * 0.08f) * 0.06f * intensity;
            _noisePhase += intensity * 18f / _sampleRate;

            float body = _noiseFilterCascade * 0.72f + _noiseFilterBloom * 0.28f;
            return body * (1f + turbulence);
        }
#endif

        static float SoftClip(float x)
        {
            const float threshold = 0.85f;
            if (x > threshold)
            {
                float t = (x - threshold) * 2f;
                return threshold + (1f - threshold) * (t / (1f + Mathf.Abs(t)));
            }
            if (x < -threshold)
            {
                float t = (-x - threshold) * 2f;
                return -threshold - (1f - threshold) * (t / (1f + Mathf.Abs(t)));
            }
            return x;
        }

        static float SfxLinear() => Mathf.Clamp01(PlayerPrefs.GetFloat(AudioPrefsKeys.SfxVolume, AudioMixDefaults.SfxLinear));

        void OnDisable()
        {
            if (_audioSource != null && _audioSource.isPlaying)
                _audioSource.Stop();
        }
    }
}
