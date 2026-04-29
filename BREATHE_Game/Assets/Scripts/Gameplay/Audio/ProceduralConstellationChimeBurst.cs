using UnityEngine;
using Breathe.Utility;

namespace Breathe.Audio
{
    /// <summary>
    /// Bright harmonic chime burst for constellation reveal / skydive landing success.
    /// WebGL: OnAudioFilterRead unsupported — pre-generates a clip in Awake and plays that instead.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class ProceduralConstellationChimeBurst : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] float _masterVolume = 0.42f;

        AudioSource _src;
        int _rate;

#if UNITY_WEBGL && !UNITY_EDITOR
        AudioClip _pregenClip;
#else
        float _cachedSfxLinear = 1f; // Cached on main thread for audio thread access
        int _samplesLeft;
        double _p1;
        double _p2;
        double _p3;
#endif

        void Awake()
        {
            _src = GetComponent<AudioSource>();
            _src.loop = false;
            _src.playOnAwake = false;
            _src.spatialBlend = 0f;

            _rate = Mathf.Max(AudioSettings.outputSampleRate, 8000);

#if UNITY_WEBGL && !UNITY_EDITOR
            _pregenClip = GenerateChimeClip();
            _src.clip = _pregenClip;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        AudioClip GenerateChimeClip()
        {
            int total = Mathf.RoundToInt(0.45f * _rate);
            float[] samples = new float[total];
            float dt = 1f / Mathf.Max(1f, _rate);

            double p1 = 0d, p2 = 0d, p3 = 0d;
            for (int i = 0; i < total; i++)
            {
                float t = Mathf.Clamp01(i / (float)total);
                float env = Mathf.Exp(-t * 4.25f);
                float v = env * _masterVolume * 0.36f;

                double dtD = dt;
                p1 += 2d * System.Math.PI * 1520d * dtD;
                p2 += 2d * System.Math.PI * 2290d * dtD;
                p3 += 2d * System.Math.PI * 3045d * dtD;

                float mix = v * ((float)(System.Math.Sin(p1) * 0.55)
                                 + (float)(System.Math.Sin(p2) * 0.32)
                                 + (float)(System.Math.Sin(p3) * 0.21));
                samples[i] = Mathf.Clamp(mix, -1f, 1f);
            }

            var clip = AudioClip.Create("ChimeBurst", total, 1, _rate, false);
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
            _p1 = _p2 = _p3 = 0d;
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
                float t = Mathf.Clamp01(elapsed / (float)total);
                float env = Mathf.Exp(-t * 4.25f);
                float v = env * _masterVolume * 0.36f * _cachedSfxLinear;

                double dtD = dt;
                _p1 += 2d * System.Math.PI * 1520d * dtD;
                _p2 += 2d * System.Math.PI * 2290d * dtD;
                _p3 += 2d * System.Math.PI * 3045d * dtD;

                float mix = v * ((float)(System.Math.Sin(_p1) * 0.55)
                                 + (float)(System.Math.Sin(_p2) * 0.32)
                                 + (float)(System.Math.Sin(_p3) * 0.21));
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
