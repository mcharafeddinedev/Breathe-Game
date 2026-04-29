using UnityEngine;
using Breathe.Utility;

namespace Breathe.Audio
{
    /// <summary>
    /// Hull / water wash plus mid rigging band — driven by breath and speed.
    /// Optional sampled ocean wave one-shots while breath is high (see ocean accent fields).
    /// NOTE: OnAudioFilterRead is NOT supported on WebGL — procedural hull/rig is silent in browser builds;
    /// ocean accent one-shots still play if assigned.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class ProceduralSailboatWindAudio : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f), Tooltip("Ocean wash + rig — keep below wind SFX so it reads as water, not vacuum.")]
        float _masterVolume = 0.27f;

        [SerializeField, Range(0.05f, 1f), Tooltip("Hull/rig oscillator and ocean-accent layer gain. Balance vs bow splash SecondaryAction one-shots.")]
        float _proceduralMixScale = 0.72f;

#if !UNITY_WEBGL || UNITY_EDITOR
        [Header("Low wash (hull / water — slow swell, not jet hiss)")]
        [SerializeField] float _washBaseHz = 165f;
        [SerializeField] float _washBreathStretch = 440f;

        [Header("Rigging band (narrower highs = less vacuum-hose)")]
        [SerializeField] float _rigBaseHz = 1120f;
        [SerializeField] float _rigSpeedStretch = 520f;
#endif

        [SerializeField] float _breathSmoothing = 9f;
        [SerializeField] float _driveSmoothing = 6f;

        [SerializeField, Tooltip("Breath must exceed this (0–1) before hull/rig opens (suppresses idle mic bleed / intro hiss).")]
        float _minBreathToOpen = 0.042f;

        [SerializeField, Tooltip("Optional Freesound / asset clip — assign in Inspector (e.g. 404762 owlstorm ocean wave).")]
        AudioClip _oceanAccentClip;
        [SerializeField, Range(0f, 1f), Tooltip("Breath (0–1) must exceed this for wave rolls — user requested ~40%.")]
        float _oceanAccentBreathGate = 0.4f;
        [SerializeField, Range(0.02f, 1.5f), Tooltip("Rough rate of stochastic wave hits while gated input stays high.")]
        float _oceanAccentRollsPerSecond = 0.22f;
        [SerializeField, Tooltip("Avoid machine-gun overlaps; jitter added after each attempted roll.")]
        float _oceanAccentCooldownMin = 1.1f;
        [SerializeField] float _oceanAccentCooldownMax = 2.9f;
        [SerializeField, Range(0f, 1f)] float _oceanAccentPeakVolume = 0.42f;
        [SerializeField, Tooltip("Vol/s toward 1 when breath is above gate (keeps swell tied to sustained blow).")]
        float _oceanAccentFadeInSpeed = 3.4f;
        [SerializeField, Tooltip("Vol/s fade when breath drops / race shell closes — fades sample smoothly.")]
        float _oceanAccentFadeOutSpeed = 4.2f;
        [SerializeField, Tooltip("Normalized clip playback time before allowing another layered hit.")]
        float _oceanAccentMinHeadroomFrac = 0.48f;

        AudioSource _audioSource;
        AudioSource _accentWaveSrc;
        int _sampleRate;

        float _smoothBreath;
        float _smoothDrive;
        float _oceanAccentCooldown;
        float _oceanAccentDryVol;
        float _cachedSfxLinear = 1f; // Cached on main thread for audio thread access

        readonly System.Random _rng = new System.Random(90210);
#if !UNITY_WEBGL || UNITY_EDITOR
        float _phaseHull;
        float _phaseRigMod;
        float _lpHull;
        float _lpRig;
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        AudioClip _webglNoiseClip;
        const int WebGLLoopSamples = 44100;
        const int WebGLSampleRate = 44100;
#endif

        void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.loop = true;
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
            _audioSource.priority = 200;

            _sampleRate = Mathf.Max(AudioSettings.outputSampleRate, 8000);

            EnsureOceanAccentChild();

#if UNITY_WEBGL && !UNITY_EDITOR
            GenerateWebGLNoiseLoop();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        void GenerateWebGLNoiseLoop()
        {
            _webglNoiseClip = AudioClip.Create("SailboatWindLoop", WebGLLoopSamples, 1, WebGLSampleRate, false);
            float[] samples = new float[WebGLLoopSamples];
            
            var rng = new System.Random(90210);
            float lpHull = 0f, lpRig = 0f;
            float dt = 1f / WebGLSampleRate;
            float hullHz = 300f;
            float rigHz = 1400f;
            float rcH = 1f / (2f * Mathf.PI * hullHz);
            float alphaH = dt / (rcH + dt);
            float rcR = 1f / (2f * Mathf.PI * rigHz);
            float alphaR = dt / (rcR + dt);
            float phase = 0f;
            
            for (int i = 0; i < WebGLLoopSamples; i++)
            {
                float wH = (float)(rng.NextDouble() * 2.0 - 1.0);
                float wR = (float)(rng.NextDouble() * 2.0 - 1.0);
                
                phase += 1.2f;
                float swell = 0.7f + 0.3f * Mathf.Sin(phase * 0.02f);
                
                lpHull += alphaH * (wH - lpHull);
                lpRig += alphaR * (wR - lpRig);
                
                float s = lpHull * 0.5f * swell + lpRig * 0.25f;
                samples[i] = Mathf.Clamp(s * 0.6f, -1f, 1f);
            }
            
            _webglNoiseClip.SetData(samples, 0);
            _audioSource.clip = _webglNoiseClip;
        }
#endif

        void EnsureOceanAccentChild()
        {
            if (_oceanAccentClip == null || _accentWaveSrc != null) return;

            var go = new GameObject("OceanAccentWaveOneShot");
            go.transform.SetParent(transform, false);
            _accentWaveSrc = go.AddComponent<AudioSource>();
            _accentWaveSrc.playOnAwake = false;
            _accentWaveSrc.loop = false;
            _accentWaveSrc.spatialBlend = 0f;
            _accentWaveSrc.clip = _oceanAccentClip;
            _accentWaveSrc.priority = 210;
            _accentWaveSrc.ignoreListenerPause = true;
            _accentWaveSrc.volume = 0f;
        }

        /// <summary>Race/coast shell: true only once CourseManager exists and the race has started or player finished.</summary>
        public void Tick(float breathPower, float speed01, bool hullRaceAudible)
        {
            // Cache SFX volume on main thread for audio thread access
            _cachedSfxLinear = SfxLinear();

            float bp = Mathf.Clamp01(breathPower);
            float tgt = 0f;
            if (hullRaceAudible && bp >= _minBreathToOpen)
                tgt = bp;

            float dt = Time.deltaTime;

            _smoothBreath = Mathf.Lerp(_smoothBreath, tgt, Mathf.Min(1f, dt * _breathSmoothing));
            _smoothDrive = Mathf.Lerp(_smoothDrive, Mathf.Clamp01(speed01),
                Mathf.Min(1f, dt * _driveSmoothing));

            float mix = Mathf.Sqrt(Mathf.Max(0f, _smoothBreath))
                        * Mathf.Lerp(0.45f, 1f, _smoothDrive);

            if (mix > 0.02f && !_audioSource.isPlaying)
                _audioSource.Play();
            else if (mix < 0.007f && _audioSource.isPlaying)
                _audioSource.Stop();

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL: modulate volume and pitch based on breath/speed
            if (_audioSource.isPlaying)
            {
                _audioSource.volume = mix * _masterVolume * _proceduralMixScale * SfxLinear();
                _audioSource.pitch = 0.85f + _smoothDrive * 0.3f + _smoothBreath * 0.15f;
            }
#endif

            TickOceanAccent(dt, hullRaceAudible, bp);
        }

        float Rng01() => Mathf.Clamp01((float)_rng.NextDouble());

        void TickOceanAccent(float dt, bool hullRaceAudible, float breath01)
        {
            EnsureOceanAccentChild();
            if (_accentWaveSrc == null || _oceanAccentClip == null) return;

            breath01 = Mathf.Clamp01(breath01);

            bool gateOpen = hullRaceAudible
                            && Mathf.Clamp01(_smoothBreath) >= _oceanAccentBreathGate
                            && breath01 >= _oceanAccentBreathGate;

            float headroom = Mathf.Clamp01((_smoothBreath - _oceanAccentBreathGate) /
                                           Mathf.Max(1e-4f, 1f - _oceanAccentBreathGate));

            float targetDry = gateOpen ? headroom : 0f;
            float fadeRate = gateOpen ? _oceanAccentFadeInSpeed : _oceanAccentFadeOutSpeed;

            _oceanAccentDryVol = Mathf.MoveTowards(_oceanAccentDryVol, targetDry, fadeRate * dt);

            float wet = Mathf.Clamp01(_oceanAccentDryVol);
            float sfxPk = SfxLinear();

            float procSc = Mathf.Clamp01(_proceduralMixScale);
            _accentWaveSrc.volume = _oceanAccentPeakVolume * wet * sfxPk * procSc;

            if (_oceanAccentDryVol <= 1e-4f)
            {
                if (_accentWaveSrc.isPlaying)
                    _accentWaveSrc.Stop();

                _oceanAccentCooldown = 0f;
                return;
            }

            float len = Mathf.Max(0.01f, _oceanAccentClip.length);
            float minAhead = Mathf.Clamp(_oceanAccentMinHeadroomFrac, 0f, 0.92f);

            bool canLayer =
                !_accentWaveSrc.isPlaying || _accentWaveSrc.time >= len * minAhead;

            _oceanAccentCooldown -= dt;

            if (!canLayer || _oceanAccentCooldown > 0f)
                return;

            float rollP = Mathf.Min(1f, _oceanAccentRollsPerSecond * dt);
            if (Rng01() > rollP)
                return;

            float span = Mathf.Max(0f, _oceanAccentCooldownMax - _oceanAccentCooldownMin);
            _oceanAccentCooldown = _oceanAccentCooldownMin + span * Rng01();

            _accentWaveSrc.pitch = Mathf.Lerp(0.93f, 1.06f, Rng01());
            _accentWaveSrc.time = 0f;
            _accentWaveSrc.volume = _oceanAccentPeakVolume * Mathf.Clamp01(_oceanAccentDryVol) * sfxPk * procSc;
            _accentWaveSrc.Play();
        }

        float SfxLinear() => Mathf.Clamp01(PlayerPrefs.GetFloat(AudioPrefsKeys.SfxVolume, AudioMixDefaults.SfxLinear));

#if !UNITY_WEBGL || UNITY_EDITOR
        void OnAudioFilterRead(float[] data, int channels)
        {
            float b = Mathf.Clamp01(_smoothBreath);
            float sp = Mathf.Clamp01(_smoothDrive);
            float throat = Mathf.Sqrt(Mathf.Max(0f, b * Mathf.Lerp(0.4f, 1f, sp)));

            float washHz = Mathf.Clamp(_washBaseHz + b * _washBreathStretch, 100f, 2200f);
            float rigHz = Mathf.Clamp(_rigBaseHz + sp * _rigSpeedStretch, 600f, 7000f);

            float procSc = Mathf.Clamp01(_proceduralMixScale);

            float vHull = throat * _masterVolume * 0.38f * procSc * _cachedSfxLinear;

            float vRig = Mathf.Pow(Mathf.Clamp01(b * Mathf.Lerp(0.55f, 1f, sp)), 1.08f) * _masterVolume * 0.2f * procSc * _cachedSfxLinear;

            float dtSample = 1f / Mathf.Max(1f, _sampleRate);

            for (int i = 0; i < data.Length; i += channels)
            {
                float s = 0f;
                bool any = throat > 1e-7f || vRig > 1e-9f;

                if (any)
                {
                    float swell = 0.66f + 0.34f * Mathf.Sin(_phaseHull * (0.024f + sp * 0.012f));
                    s += HullSample(washHz, dtSample) * vHull * swell;
                    s += RigSample(rigHz, b, dtSample) * vRig;
                }

                s = SoftClip(s);
                for (int c = 0; c < channels; c++)
                    data[i + c] = s;
            }
        }

        float HullSample(float cutoffHz, float dt)
        {
            float white = (float)(_rng.NextDouble() * 2.0 - 1.0);
            float rc = 1f / (2f * Mathf.PI * Mathf.Clamp(cutoffHz, 90f, 2400f));
            float alpha = dt / (rc + dt);
            _phaseHull += Mathf.Clamp(cutoffHz * 0.000018f + 0.8f + _smoothDrive * 0.4f, 0f, 2f);

            float lfo = Mathf.Sin(_phaseHull * 0.08f + cutoffHz * 0.0012f);
            float inW = white * (1f + lfo * 0.06f);

            _lpHull += alpha * (inW - _lpHull);

            float rumble = Mathf.Sin(_phaseHull * (0.12f + _smoothBreath * 0.06f));

            return _lpHull * (1f + rumble * 0.05f * Mathf.Clamp(cutoffHz / 520f, 0.4f, 1.2f));
        }

        float RigSample(float centerHz, float breath, float dt)
        {
            float white = (float)(_rng.NextDouble() * 2.0 - 1.0);
            float hz = Mathf.Clamp(centerHz, 720f, 5200f);
            float rc = 1f / (2f * Mathf.PI * hz);
            float alphaR = Mathf.Clamp01(dt / (rc + dt));
            _phaseRigMod += 2.1f;
            float whine = Mathf.Sin(_phaseRigMod * Mathf.Lerp(0.045f, 0.082f, breath));
            float shaped = white * (0.94f + 0.12f * whine);
            _lpRig += alphaR * (shaped - _lpRig);

            float edge = Mathf.Sin(_phaseRigMod * 1.009f) * 0.025f;

            return _lpRig * (1f + edge) * Mathf.Lerp(0.5f, 1f, breath);
        }
#endif

        static float SoftClip(float x)
        {
            const float limit = 0.9f;

            float ax = Mathf.Abs(x);

            return Mathf.Sign(x) * Mathf.Min(ax, limit + (Mathf.Max(0f, ax - limit) / (6f + (ax - limit) * 8f)));

        }

        void OnDisable()
        {
            if (_audioSource != null && _audioSource.isPlaying)
                _audioSource.Stop();
            if (_accentWaveSrc != null && _accentWaveSrc.isPlaying)
                _accentWaveSrc.Stop();
        }
    }
}
