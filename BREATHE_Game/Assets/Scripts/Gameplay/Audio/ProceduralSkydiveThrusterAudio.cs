using UnityEngine;
using Breathe.Utility;

namespace Breathe.Audio
{
    /// <summary>
    /// Rocket thruster: sub rumble + low roar + brighter core — distinct from balloon / wind.
    /// Desktop/Editor: uses OnAudioFilterRead for real-time synthesis.
    /// WebGL: uses pre-generated noise loop with volume/pitch modulation.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class ProceduralSkydiveThrusterAudio : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f), Tooltip("Sub rumble adds level; keep modest in Inspector.")]
        float _masterVolume = 0.24f;
        [SerializeField, Tooltip("Breath must clear this (0–1) to open thruster (suppresses spawn/mic bleed).")]
        float _minBreathToOpen = 0.05f;

#if !UNITY_WEBGL || UNITY_EDITOR
        [Header("Tone — jet pack: prominent sub + roar + bright core")]
        [SerializeField] float _coreBaseHz = 1180f;
        [SerializeField] float _thrustStretchHz = 2600f;
        [SerializeField, Range(0f, 1f), Tooltip("Blend white into roar LP = fuller bass-mid body.")]
        float _rumbleBlend = 0.52f;
#endif

        AudioSource _src;
        int _rate;
#if !UNITY_WEBGL || UNITY_EDITOR
        readonly System.Random _rng = new System.Random(44421);

        float _lpCore;
        float _lpRoar;
        float _fmPhase;
        float _subPhase;
#endif

        float _smoothBreath;
        float _smoothThrustNorm;
        float _cachedSfxLinear = 1f; // Cached on main thread for audio thread access

        [SerializeField] float _smooth = 14f;

#if UNITY_WEBGL && !UNITY_EDITOR
        AudioClip _webglNoiseClip;
        const int WebGLLoopSamples = 44100;
        const int WebGLSampleRate = 44100;
#endif

        void Awake()
        {
            _src = GetComponent<AudioSource>();
            _src.loop = true;
            _src.playOnAwake = false;
            _src.spatialBlend = 0f;
            _src.priority = 200;
            _rate = Mathf.Max(AudioSettings.outputSampleRate, 8000);

#if UNITY_WEBGL && !UNITY_EDITOR
            GenerateWebGLNoiseLoop();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        void GenerateWebGLNoiseLoop()
        {
            _webglNoiseClip = AudioClip.Create("ThrusterNoiseLoop", WebGLLoopSamples, 1, WebGLSampleRate, false);
            float[] samples = new float[WebGLLoopSamples];
            
            var rng = new System.Random(44421);
            float lpCore = 0f, lpRoar = 0f;
            float dt = 1f / WebGLSampleRate;
            float cutoff = 1800f;
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float a = dt / (rc + dt);
            float roarFc = 180f;
            float rc2 = 1f / (2f * Mathf.PI * roarFc);
            float a2 = dt / (rc2 + dt);
            float subPhase = 0f;
            
            for (int i = 0; i < WebGLLoopSamples; i++)
            {
                float w = (float)(rng.NextDouble() * 2f - 1f);
                lpCore += a * (w - lpCore);
                float roar = Mathf.Lerp(w, lpCore, 0.52f);
                lpRoar += a2 * (roar - lpRoar);
                
                subPhase += 6.2831853f * 80f * dt;
                float sub = Mathf.Sin(subPhase) * 0.4f;
                
                float s = lpCore * 0.26f + lpRoar * 0.64f + sub;
                samples[i] = Mathf.Clamp(s * 0.5f, -1f, 1f);
            }
            
            _webglNoiseClip.SetData(samples, 0);
            _src.clip = _webglNoiseClip;
        }
#endif

        /// <summary>Clear carry-over smoothing (call when a new diver spawns so previous breath/thrust tails do not hiss).</summary>
        public void ResetSmoothing()
        {
            _smoothBreath = 0f;
            _smoothThrustNorm = 0f;
#if !UNITY_WEBGL || UNITY_EDITOR
            _lpCore = _lpRoar = 0f;
            _fmPhase = 0f;
            _subPhase = 0f;
#endif

            if (_src != null && _src.isPlaying)
                _src.Stop();
        }

        public void Tick(float breath01, float thrustBuildupNormalized, bool active)
        {
            // Cache SFX volume on main thread for audio thread access
            _cachedSfxLinear = SfxLinear();

            float bp = Mathf.Clamp01(breath01);

            float bt = 0f;
            if (active && bp >= _minBreathToOpen)
                bt = Mathf.Clamp01(bp);

            _smoothBreath = Mathf.Lerp(_smoothBreath, bt, Mathf.Min(1f, Time.deltaTime * _smooth));

            float thrustTgt = active ? Mathf.Clamp01(thrustBuildupNormalized) : 0f;
            _smoothThrustNorm = Mathf.Lerp(_smoothThrustNorm, thrustTgt,
                Mathf.Min(1f, Time.deltaTime * _smooth));

            float drive = Mathf.Sqrt(Mathf.Max(0f, _smoothBreath)) * Mathf.Lerp(0.35f, 1f, _smoothThrustNorm);

            if (drive > 0.018f && !_src.isPlaying)
                _src.Play();
            else if (drive < 0.01f && _src.isPlaying)
                _src.Stop();

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL: modulate volume and pitch based on breath/thrust
            if (_src.isPlaying)
            {
                _src.volume = drive * _masterVolume * 0.8f * _cachedSfxLinear;
                _src.pitch = 0.7f + _smoothThrustNorm * 0.5f + _smoothBreath * 0.2f;
            }
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        void OnAudioFilterRead(float[] data, int channels)
        {
            float b = Mathf.Clamp01(_smoothBreath);
            float tn = Mathf.Clamp01(_smoothThrustNorm);
            float cutoff = Mathf.Clamp(_coreBaseHz + tn * _thrustStretchHz, 720f, 9800f);
            float dt = 1f / Mathf.Max(1f, _rate);

            float vol = Mathf.Sqrt(Mathf.Max(0f, b * Mathf.Lerp(0.35f, 1f, tn))) * _masterVolume * 0.34f * _cachedSfxLinear;

            for (int i = 0; i < data.Length; i += channels)
            {
                float w = (float)(_rng.NextDouble() * 2f - 1f);

                float rc = 1f / (2f * Mathf.PI * cutoff);
                float a = Mathf.Clamp01(dt / (rc + dt));
                float roarFc = Mathf.Clamp(cutoff * 0.14f + 62f, 42f, 1400f);
                float rc2 = 1f / (2f * Mathf.PI * roarFc);
                float a2 = Mathf.Clamp01(dt / (rc2 + dt));

                _lpCore += a * (w - _lpCore);
                float roar = Mathf.Lerp(w, _lpCore, _rumbleBlend);
                _lpRoar += a2 * (roar - _lpRoar);

                _fmPhase += 2f * Mathf.PI * Mathf.Clamp(72f + tn * 165f + b * 75f, 18f, 620f) * dt;
                float fm = Mathf.Sin(_fmPhase + tn * Mathf.Sin(_fmPhase * 0.31f));

                float subHz = Mathf.Clamp(42f + tn * 118f + b * 62f, 32f, 190f);
                _subPhase += 6.2831853f * subHz * dt;
                float subAmp = Mathf.Sqrt(Mathf.Max(1e-6f, b)) * Mathf.Lerp(0.22f, 1f, tn);
                float sub = Mathf.Sin(_subPhase) * (0.62f + 0.38f * Mathf.Sin(_subPhase * 0.50f));
                sub *= subAmp * 1.05f;

                float mids = _lpCore * 0.26f + _lpRoar * 0.64f;
                mids *= 1f + fm * (0.07f + b * 0.06f);

                float s = mids + sub;
                s = SoftClip(s * vol);

                for (int c = 0; c < channels; c++)
                    data[i + c] = s;
            }
        }
#endif

        static float SoftClip(float x)
        {
            float ax = Mathf.Abs(x);
            float t = Mathf.Min(ax, 0.92f);
            float e = Mathf.Max(ax - 0.92f, 0f);
            return Mathf.Sign(x) * (t + e / (1f + e * 4f));
        }

        static float SfxLinear() => Mathf.Clamp01(PlayerPrefs.GetFloat(AudioPrefsKeys.SfxVolume, AudioMixDefaults.SfxLinear));

        void OnDisable()
        {
            if (_src != null && _src.isPlaying)
                _src.Stop();
        }
    }
}
