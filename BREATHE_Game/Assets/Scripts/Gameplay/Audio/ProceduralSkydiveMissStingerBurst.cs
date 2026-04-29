using UnityEngine;
using Breathe.Utility;

namespace Breathe.Audio
{
    /// <summary>
    /// Short procedural "failure" sting — counterpart to <see cref="ProceduralConstellationChimeBurst"/>
    /// (descending glide + rough partials vs bright harmonic stack).
    /// WebGL: OnAudioFilterRead unsupported — pre-generates a clip in Awake and plays that instead.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class ProceduralSkydiveMissStingerBurst : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] float _masterVolume = 0.54f;

        [SerializeField, Tooltip("Fade-in (~ms). Softens onset so it does not snap or sound clipped.")]
        float _attackMilliseconds = 38f;

        AudioSource _src;
        int _rate;

#if UNITY_WEBGL && !UNITY_EDITOR
        AudioClip _pregenClip;
#else
        float _cachedSfxLinear = 1f; // Cached on main thread for audio thread access
        int _samplesLeft;
        double _glidePhase;
        double _roughPhase;
#endif

        void Awake()
        {
            _src = GetComponent<AudioSource>();
            _src.loop = false;
            _src.playOnAwake = false;
            _src.spatialBlend = 0f;

            _rate = Mathf.Max(AudioSettings.outputSampleRate, 8000);

#if UNITY_WEBGL && !UNITY_EDITOR
            _pregenClip = GenerateMissClip();
            _src.clip = _pregenClip;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        AudioClip GenerateMissClip()
        {
            int total = Mathf.RoundToInt(0.45f * _rate);
            float[] samples = new float[total];
            float dt = 1f / Mathf.Max(1f, _rate);
            int attackSamples = Mathf.Max(4, Mathf.RoundToInt((_attackMilliseconds * 0.001f) * _rate));

            double glidePhase = 0d, roughPhase = 0d;
            for (int i = 0; i < total; i++)
            {
                float u = Mathf.Clamp01(i / (float)total);
                float env = Mathf.Exp(-u * 5.9f);

                float a = Mathf.Clamp01((i + 1f) / attackSamples);
                float attack = a * a * (3f - 2f * a);

                float gain = env * attack * _masterVolume * 0.5f;

                float hzSlide = Mathf.Lerp(980f, 380f, u * u);
                glidePhase += 6.283185307179586d * hzSlide * dt;
                float d1 = (float)System.Math.Sin(glidePhase);

                float hzRough = Mathf.Lerp(520f, 310f, u);
                roughPhase += 6.283185307179586d * hzRough * dt;
                float d2 = (float)System.Math.Sin(roughPhase * 2.06 + glidePhase * 0.13);

                float grit = Mathf.Sin((float)(glidePhase * 0.5 + roughPhase * 0.7)) * (0.12f + 0.06f * (1f - u));

                float mix = gain * (
                    d1 * (0.44f + 0.06f * Mathf.Sin(u * Mathf.PI * 12f))
                    + d2 * 0.35f
                    + grit);

                samples[i] = Mathf.Clamp(mix, -1f, 1f);
            }

            var clip = AudioClip.Create("MissStinger", total, 1, _rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public void Trigger()
        {
            _src.Stop();
            _src.time = 0f;
            _src.volume = SfxLinear();
            _src.Play();
        }
#else
        public void Trigger()
        {
            // Cache SFX volume on main thread for audio thread access
            _cachedSfxLinear = SfxLinear();
            _samplesLeft = Mathf.RoundToInt(0.45f * _rate);
            _glidePhase = _roughPhase = 0d;
            if (!_src.isPlaying)
                _src.Play();
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            float dt = 1f / Mathf.Max(1f, _rate);

            int ch = Mathf.Max(1, channels);
            int n = data.Length / ch;

            if (_samplesLeft <= 0)
            {
                for (int i = 0; i < data.Length; i++)
                    data[i] = 0f;
                return;
            }

            int total = Mathf.Max(1, Mathf.RoundToInt(0.45f * _rate));

            for (int s = 0; s < n; s++)
            {
                if (_samplesLeft <= 0)
                {
                    for (int j = s * ch; j < data.Length; j++)
                        data[j] = 0f;
                    return;
                }

                int elapsed = total - _samplesLeft;
                float u = Mathf.Clamp01(elapsed / (float)total);
                float env = Mathf.Exp(-u * 5.9f);

                int attackSamples = Mathf.Max(4, Mathf.RoundToInt((_attackMilliseconds * 0.001f) * _rate));
                float a = Mathf.Clamp01((elapsed + 1f) / attackSamples);
                float attack = a * a * (3f - 2f * a);

                float gain = env * attack * _masterVolume * 0.5f * _cachedSfxLinear;
                float v = gain;

                float hzSlide = Mathf.Lerp(980f, 380f, u * u);
                _glidePhase += 6.283185307179586d * hzSlide * dt;
                float d1 = (float)System.Math.Sin(_glidePhase);

                float hzRough = Mathf.Lerp(520f, 310f, u);
                _roughPhase += 6.283185307179586d * hzRough * dt;
                float d2 = (float)System.Math.Sin(_roughPhase * 2.06 + _glidePhase * 0.13);

                float grit = Mathf.Sin((float)(_glidePhase * 0.5 + _roughPhase * 0.7)) * (0.12f + 0.06f * (1f - u));

                float mix = v * (
                    d1 * (0.44f + 0.06f * Mathf.Sin(u * Mathf.PI * 12f))
                    + d2 * 0.35f
                    + grit);

                mix = Mathf.Clamp(mix, -1f, 1f);

                int bi = s * ch;
                for (int c = 0; c < ch; c++)
                    data[bi + c] = mix;

                _samplesLeft--;
            }
        }
#endif

        void OnDisable()
        {
            if (_src != null && _src.isPlaying)
                _src.Stop();
        }

        static float SfxLinear() => Mathf.Clamp01(PlayerPrefs.GetFloat(AudioPrefsKeys.SfxVolume, AudioMixDefaults.SfxLinear));
    }
}
