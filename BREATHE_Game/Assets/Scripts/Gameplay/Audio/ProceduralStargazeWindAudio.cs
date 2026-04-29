using UnityEngine;
using Breathe.Utility;

namespace Breathe.Audio
{
    /// <summary>
    /// Night-sky wind bed for Stargaze: gust band + mist band.
    /// Desktop/Editor: uses OnAudioFilterRead for real-time synthesis.
    /// WebGL: uses pre-generated noise loop with volume/pitch modulation.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class ProceduralStargazeWindAudio : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f), Tooltip("Overall level — bed should sit under breath-forward gust.")]
        float _masterVolume = 0.09f;

#if !UNITY_WEBGL || UNITY_EDITOR
        [Header("Gust band (high-air clearing — flutter reads as gusts, not a hose)")]
        [SerializeField] float _gustCutoffBase = 1100f;
        [SerializeField] float _gustBreathStretch = 1220f;
        [SerializeField, Tooltip("Keeps gust airy, not dull or machine-harsh.")]
        float _gustCutoffCapHz = 6200f;

        [Header("Mist band (Pink-ish second pole = sky hush vs boat wash)")]
        [SerializeField, Tooltip("Higher = less tub — distinct from hull wash.")]
        float _mistCutoffHz = 880f;
        [SerializeField]
        float _mistOutputWeight = 0.34f;
        [SerializeField]
        float _gustOutputWeight = 0.44f;

        [Header("Air band (mid-high — night sky 'air', not white noise)")]
        [SerializeField, Tooltip("Separate breathy 'air' layer, low-passed here.")]
        float _airCutoffHz = 3600f;
        [SerializeField, Range(0f, 1f)]
        float _airOutputWeight = 0.35f;

        [SerializeField, Tooltip("Slow sweep on gust cutoff (keep modest to avoid woozy pipe).")]
        float _lfoRateHz = 0.42f;
#endif

        AudioSource _src;
        int _rate;

        float _smoothGust;
        float _smoothMist;
        float _cachedSfxLinear = 1f; // Cached on main thread for audio thread access

        [SerializeField] float _smooth = 11f;

#if !UNITY_WEBGL || UNITY_EDITOR
        readonly System.Random _rng = new System.Random(55123);

        float _lpGust;
        float _lpMist;
        float _lpMistSlow;
        float _lpAir;

        float _lfoPhase;
        float _flutterPhase;
#endif

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
            _rate = Mathf.Max(AudioSettings.outputSampleRate, 8000);

#if UNITY_WEBGL && !UNITY_EDITOR
            GenerateWebGLNoiseLoop();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        void GenerateWebGLNoiseLoop()
        {
            _webglNoiseClip = AudioClip.Create("StargazeWindLoop", WebGLLoopSamples, 1, WebGLSampleRate, false);
            float[] samples = new float[WebGLLoopSamples];
            
            var rng = new System.Random(55123);
            float lpGust = 0f, lpMist = 0f, lpAir = 0f;
            float dt = 1f / WebGLSampleRate;
            float gustHz = 1800f;
            float mistHz = 880f;
            float airHz = 3600f;
            float rcG = 1f / (2f * Mathf.PI * gustHz);
            float rcM = 1f / (2f * Mathf.PI * mistHz);
            float rcA = 1f / (2f * Mathf.PI * airHz);
            float aG = dt / (rcG + dt);
            float aM = dt / (rcM + dt);
            float aA = dt / (rcA + dt);
            float lfoPhase = 0f;
            
            for (int i = 0; i < WebGLLoopSamples; i++)
            {
                float wG = (float)(rng.NextDouble() * 2f - 1f);
                float wM = (float)(rng.NextDouble() * 2f - 1f);
                float wA = (float)(rng.NextDouble() * 2f - 1f);
                
                lfoPhase += 6.2831853f * 0.42f * dt;
                float lfoMod = 1f + Mathf.Sin(lfoPhase) * 0.05f;
                
                lpGust += aG * lfoMod * (wG - lpGust);
                lpMist += aM * (wM - lpMist);
                lpAir += aA * (wA - lpAir);
                
                float s = lpGust * 0.44f + lpMist * 0.34f + lpAir * 0.35f;
                samples[i] = Mathf.Clamp(s * 0.4f, -1f, 1f);
            }
            
            _webglNoiseClip.SetData(samples, 0);
            _src.clip = _webglNoiseClip;
        }
#endif

        public void Tick(float gust01, float mist01)
        {
            // Cache SFX volume on main thread for audio thread access
            _cachedSfxLinear = SfxLinear();

            float dt = Mathf.Min(1f, Time.deltaTime * _smooth);
            _smoothGust = Mathf.Lerp(_smoothGust, Mathf.Clamp01(gust01), dt);
            _smoothMist = Mathf.Lerp(_smoothMist, Mathf.Clamp01(mist01), dt);

            float v = Mathf.Max(_smoothGust, _smoothMist * 0.88f);
            if (v > 0.008f && !_src.isPlaying) _src.Play();
            else if (v < 0.005f && _src.isPlaying) _src.Stop();

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL: modulate volume and pitch based on gust/mist
            if (_src.isPlaying)
            {
                float presence = Mathf.Clamp01(Mathf.Max(_smoothGust, _smoothMist));
                float gustForward = Mathf.Sqrt(Mathf.Clamp01(_smoothGust));
                float airyBed = Mathf.Lerp(0.38f, 0.96f, gustForward);
                _src.volume = presence * _masterVolume * airyBed * SfxLinear();
                _src.pitch = 0.9f + _smoothGust * 0.2f + _smoothMist * 0.1f;
            }
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        void OnAudioFilterRead(float[] data, int channels)
        {
            float g = Mathf.Clamp01(_smoothGust);
            float m = Mathf.Clamp01(_smoothMist);
            float dt = 1f / Mathf.Max(1f, _rate);

            float gustNorm = Mathf.Sqrt(Mathf.Max(1e-5f, g));
            float mistNorm = Mathf.Sqrt(Mathf.Max(1e-5f, m));
            float presence = Mathf.Clamp01(Mathf.Max(g, m));

            for (int i = 0; i < data.Length; i += channels)
            {
                _lfoPhase += 6.2831853f / _rate * _lfoRateHz * Mathf.Lerp(0.82f, 1.06f, m);
                float lfoMod = 1f
                               + Mathf.Sin(_lfoPhase) * 0.055f * (0.35f + g)
                               + Mathf.Sin(_lfoPhase * 0.47f + g * 1.9f + m * 2.7f)
                               * 0.04f;

                _flutterPhase += dt * Mathf.Lerp(6.2f, 24f, Mathf.Clamp01(g * 0.95f + m * 0.35f));
                float flutter = 1f
                                + Mathf.Sin(_flutterPhase) * 0.034f
                                + Mathf.Sin(_flutterPhase * 2.63f + m * 1.9f) * 0.022f;

                float gustFc = Mathf.Clamp(
                    (_gustCutoffBase + g * _gustBreathStretch) * lfoMod * flutter,
                    480f,
                    _gustCutoffCapHz);

                float w = (float)(_rng.NextDouble() * 2f - 1f);
                float aGust = Alpha(gustFc, dt);
                _lpGust += aGust * (w - _lpGust);

                float mw = (float)(_rng.NextDouble() * 2f - 1f);
                float aM = Alpha(_mistCutoffHz, dt);
                _lpMist += aM * (mw - _lpMist);
                float msLow = Mathf.Clamp(_mistCutoffHz * 0.34f, 220f, 520f);
                _lpMistSlow += Alpha(msLow, dt) * (_lpMist - _lpMistSlow);
                float mistSig = _lpMist * 0.56f + _lpMistSlow * 0.44f;

                float aw = (float)(_rng.NextDouble() * 2f - 1f);
                _lpAir += Alpha(_airCutoffHz, dt) * (aw - _lpAir);

                float airPresence = Mathf.Clamp01(0.5f * m + 0.72f * g);
                float airDrive = Mathf.Sqrt(Mathf.Max(1e-5f, airPresence));

                float gustOut = _lpGust * gustNorm * _gustOutputWeight;
                float mistOut = mistSig * mistNorm * _mistOutputWeight;
                float airOut = _lpAir * airDrive * _airOutputWeight;

                float layer = gustOut + mistOut + airOut;

                float drive = Mathf.Lerp(presence * 0.36f + m * 0.34f, 0.42f + presence * 0.5f,
                    Mathf.Sqrt(Mathf.Clamp01(presence + m * 0.28f)));
                drive = Mathf.Clamp01(drive);
                float gustForward = Mathf.Sqrt(Mathf.Clamp01(g));
                float airyBed = Mathf.Lerp(0.38f, 0.96f, gustForward);
                float vol = drive * _masterVolume * airyBed * Mathf.Lerp(0.42f, 0.78f,
                    Mathf.Sqrt(Mathf.Clamp01(0.12f + 0.88f * presence))) * _cachedSfxLinear;

                float s = SoftClip(layer * vol);
                for (int c = 0; c < channels; c++)
                    data[i + c] = s;
            }
        }
#endif

        static float Alpha(float hz, float dt)
        {
            float hzC = Mathf.Clamp(hz, 120f, 9800f);
            float rc = 1f / (2f * Mathf.PI * hzC);
            return Mathf.Clamp01(dt / (rc + dt));
        }

        static float SoftClip(float x)
        {
            const float knee = 0.85f;

            float ax = Mathf.Abs(x);
            float d = Mathf.Max(ax - knee, 0f);

            float y = Mathf.Min(ax, knee + d / (8f + d * 3.2f));
            return Mathf.Sign(x) * y;
        }

        static float SfxLinear() => Mathf.Clamp01(PlayerPrefs.GetFloat(AudioPrefsKeys.SfxVolume, AudioMixDefaults.SfxLinear));

        void OnDisable()
        {
            if (_src != null && _src.isPlaying) _src.Stop();
        }
    }
}
